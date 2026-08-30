// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Real mouse/trackpad support for iPadOS. Unity's iOS player has NONE: its runtime never
// references GCMouse, UIHoverGestureRecognizer or prefersPointerLocked (verified against
// libiPhone-lib.a in 2022.3.62f3 and 6000.3.23f1). A pointer reaches the game only as
// "indirect pointer" touches when a button is clicked, hover produces nothing, and
// Cursor.lockState is a silent no-op. So on an iPad with a Magic Keyboard the camera
// stopped at the screen edge, attacks were impossible and the arrow wandered off into
// the system UI.
//
// This plugin supplies what Unity lacks, through the GameController framework:
//   - GCMouse        raw movement deltas, buttons and scroll (iOS 14+). Delivered whether
//                    or not the pointer is locked.
//   - UIHoverGesture the pointer's position while it is unlocked (menus), normalised so
//                    the C# side never has to agree with UIKit about contentScaleFactor.
//   - Pointer lock   an override of prefersPointerLocked installed at runtime on Unity's
//                    own view controller class, so the game can hide and capture the
//                    pointer during play and hand it back for menus. iPadOS treats the
//                    request as advisory (full-screen, foreground scene only), so the
//                    actual state is reported separately.
//
// Everything is main-thread: GCMouse handlers are pinned to the main queue and Unity's
// script loop runs there too, so the accumulators need no locking.
//
// Place in Assets/Plugins/iOS/  -  requires GameController.framework (added by
// MobileIOSPostProcess).

#import <UIKit/UIKit.h>
#import <GameController/GameController.h>
#import <objc/runtime.h>

extern "C" UIViewController* UnityGetGLViewController(void);
extern "C" UIView* UnityGetGLView(void);

static BOOL  initialised = NO;
static BOOL  lockOverrideInstalled = NO;
static BOOL  wantLock = NO;

static float accumDX = 0.0f;
static float accumDY = 0.0f;
static float accumScroll = 0.0f;
static int   buttonMask = 0;          // 1 = left, 2 = right, 4 = middle, 8 = any auxiliary
static int   auxHeld = 0;             // count of auxiliary buttons currently down

static BOOL  hoverValid = NO;
static float hoverNX = 0.5f;
static float hoverNY = 0.5f;

static int   attachedMice = 0;

// ---------------------------------------------------------------- hover target

@interface DFPointerHoverTarget : NSObject
- (void)hover:(UIHoverGestureRecognizer *)recognizer;
@end

@implementation DFPointerHoverTarget
- (void)hover:(UIHoverGestureRecognizer *)recognizer
{
    UIView *view = recognizer.view;
    if (view == nil)
        return;

    switch (recognizer.state)
    {
        case UIGestureRecognizerStateBegan:
        case UIGestureRecognizerStateChanged:
        {
            CGPoint p = [recognizer locationInView:view];
            CGSize s = view.bounds.size;
            if (s.width > 0 && s.height > 0)
            {
                hoverNX = (float)(p.x / s.width);
                hoverNY = 1.0f - (float)(p.y / s.height);   // UIKit is top-left; Unity is bottom-left
                hoverValid = YES;
            }
            break;
        }
        default:
            // Ended/cancelled: keep the last position. The arrow has not gone anywhere the
            // game can see, and a cursor that snaps to centre on every exit is worse.
            break;
    }
}
@end

static DFPointerHoverTarget *hoverTarget = nil;
static UIHoverGestureRecognizer *hoverRecognizer = nil;

// ---------------------------------------------------------------- pointer lock

// Installed as -[<UnityViewController> prefersPointerLocked]. Reads a static so the C#
// side can flip it without touching the controller.
static BOOL DFPointerPrefersLockedIMP(id self, SEL _cmd)
{
    return wantLock;
}

static void DFPointerInstallLockOverride()
{
    if (lockOverrideInstalled)
        return;

    UIViewController *vc = UnityGetGLViewController();
    if (vc == nil)
        return;

    Class cls = object_getClass(vc);
    SEL sel = @selector(prefersPointerLocked);

    // UnityDefaultViewController does not implement this (checked in both trampolines), so
    // class_addMethod adds an override above UIViewController's default NO. If a future
    // Unity does implement it, replace that implementation instead.
    if (!class_addMethod(cls, sel, (IMP)DFPointerPrefersLockedIMP, "c@:"))
    {
        Method m = class_getInstanceMethod(cls, sel);
        if (m != NULL)
            method_setImplementation(m, (IMP)DFPointerPrefersLockedIMP);
    }

    lockOverrideInstalled = YES;
    NSLog(@"[DFPointer] pointer-lock override installed on %@", NSStringFromClass(cls));
}

static void DFPointerRequestLockUpdate()
{
    if (@available(iOS 14.0, *))
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            UIViewController *vc = UnityGetGLViewController();
            if (vc != nil)
                [vc setNeedsUpdateOfPrefersPointerLocked];
        });
    }
}

// ---------------------------------------------------------------- GCMouse

static void DFPointerAttachMouse(GCMouse *mouse) API_AVAILABLE(ios(14.0))
{
    GCMouseInput *input = mouse.mouseInput;
    if (input == nil)
        return;

    mouse.handlerQueue = dispatch_get_main_queue();

    input.mouseMovedHandler = ^(GCMouseInput *m, float deltaX, float deltaY) {
        accumDX += deltaX;
        accumDY += deltaY;
    };

    input.leftButton.pressedChangedHandler = ^(GCControllerButtonInput *b, float value, BOOL pressed) {
        if (pressed) buttonMask |= 1; else buttonMask &= ~1;
    };
    if (input.rightButton != nil)
    {
        input.rightButton.pressedChangedHandler = ^(GCControllerButtonInput *b, float value, BOOL pressed) {
            if (pressed) buttonMask |= 2; else buttonMask &= ~2;
        };
    }
    if (input.middleButton != nil)
    {
        input.middleButton.pressedChangedHandler = ^(GCControllerButtonInput *b, float value, BOOL pressed) {
            if (pressed) buttonMask |= 4; else buttonMask &= ~4;
        };
    }

    // Side buttons, and any wheel-click the device reports as auxiliary rather than
    // middle. Never an action - but a click on one MUST register as a pointer button, or
    // iPadOS's touch for that click is taken for a finger and the touch HUD comes back
    // (device-proven on the first mouse build: holding a side button unlocked the pointer).
    if (input.auxiliaryButtons != nil)
    {
        for (GCControllerButtonInput *aux in input.auxiliaryButtons)
        {
            aux.pressedChangedHandler = ^(GCControllerButtonInput *b, float value, BOOL pressed) {
                if (pressed) auxHeld++; else auxHeld = MAX(auxHeld - 1, 0);
                if (auxHeld > 0) buttonMask |= 8; else buttonMask &= ~8;
            };
        }
    }

    // "Scroll is a dpad with undefined range" - accumulate and let the C# side turn it
    // into classic-UI steps.
    input.scroll.valueChangedHandler = ^(GCControllerDirectionPad *dpad, float xValue, float yValue) {
        accumScroll += yValue;
    };

    attachedMice++;
    NSLog(@"[DFPointer] mouse attached: %@ (right=%d middle=%d aux=%lu)",
          mouse.vendorName ?: @"?", input.rightButton != nil, input.middleButton != nil,
          (unsigned long)input.auxiliaryButtons.count);
}

static void DFPointerAttachAll() API_AVAILABLE(ios(14.0))
{
    for (GCMouse *mouse in [GCMouse mice])
        DFPointerAttachMouse(mouse);
}

// ---------------------------------------------------------------- exports

// ---------------------------------------------------------------- GCKeyboard
//
// Unity's iOS player reads hardware keyboards through UIKeyCommand (UnityView+Keyboard.mm),
// which only reports key PRESSES with the system's auto-repeat timing and never a release -
// Unity fakes the held state with timers. The result is a half-second lag before a walk
// starts and a stutter while the key is held (device report). GCKeyboard (iOS 14) delivers
// true down/up per key, so the C# side reads key state from here instead.
static BOOL keyDown[256];
static int  keysHeld = 0;
static int  attachedKeyboards = 0;

static void DFKeyboardClear()
{
    memset(keyDown, 0, sizeof(keyDown));
    keysHeld = 0;
}

static void DFKeyboardAttach(GCKeyboard *keyboard) API_AVAILABLE(ios(14.0))
{
    GCKeyboardInput *input = keyboard.keyboardInput;
    if (input == nil)
        return;

    input.keyChangedHandler = ^(GCKeyboardInput *kb, GCControllerButtonInput *key, GCKeyCode keyCode, BOOL pressed) {
        if (keyCode < 0 || keyCode >= 256)
            return;
        BOOL now = pressed ? YES : NO;
        if (keyDown[keyCode] == now)
            return;
        keyDown[keyCode] = now;
        keysHeld += now ? 1 : -1;
        if (keysHeld < 0)
            keysHeld = 0;
    };
    attachedKeyboards++;
    NSLog(@"[DFPointer] keyboard attached: %@", keyboard.vendorName ?: @"?");
}

extern "C" {

/// iOS 14 introduced GCMouse and pointer lock together. Below that this plugin is inert
/// and the game behaves as it did before it existed.
bool DFPointerSupported()
{
    if (@available(iOS 14.0, *))
        return true;
    return false;
}

void DFPointerInit()
{
    if (initialised || !DFPointerSupported())
        return;
    initialised = YES;

    if (@available(iOS 14.0, *))
    {
        NSNotificationCenter *nc = [NSNotificationCenter defaultCenter];

        [nc addObserverForName:GCMouseDidConnectNotification object:nil queue:[NSOperationQueue mainQueue]
                    usingBlock:^(NSNotification *note) {
            GCMouse *mouse = note.object;
            if (mouse != nil)
                DFPointerAttachMouse(mouse);
        }];

        [nc addObserverForName:GCMouseDidDisconnectNotification object:nil queue:[NSOperationQueue mainQueue]
                    usingBlock:^(NSNotification *note) {
            attachedMice = MAX(attachedMice - 1, 0);
            buttonMask = 0;       // a button held at unplug must not stay held forever
            auxHeld = 0;
            NSLog(@"[DFPointer] mouse disconnected (%d remain)", attachedMice);
        }];

        [nc addObserverForName:GCKeyboardDidConnectNotification object:nil queue:[NSOperationQueue mainQueue]
                    usingBlock:^(NSNotification *note) {
            GCKeyboard *kb = note.object;
            if (kb != nil)
                DFKeyboardAttach(kb);
        }];
        [nc addObserverForName:GCKeyboardDidDisconnectNotification object:nil queue:[NSOperationQueue mainQueue]
                    usingBlock:^(NSNotification *note) {
            attachedKeyboards = MAX(attachedKeyboards - 1, 0);
            DFKeyboardClear();      // a key held at unplug must not stay held forever
            NSLog(@"[DFPointer] keyboard disconnected (%d remain)", attachedKeyboards);
        }];
        // Backgrounding drops key-up events; start clean when the app returns.
        [nc addObserverForName:UIApplicationWillResignActiveNotification object:nil queue:[NSOperationQueue mainQueue]
                    usingBlock:^(NSNotification *note) { DFKeyboardClear(); }];
        if ([GCKeyboard coalescedKeyboard] != nil)
            DFKeyboardAttach([GCKeyboard coalescedKeyboard]);

        [nc addObserverForName:UIPointerLockStateDidChangeNotification object:nil queue:[NSOperationQueue mainQueue]
                    usingBlock:^(NSNotification *note) {
            UIScene *scene = note.userInfo[UIPointerLockStateSceneUserInfoKey];
            NSLog(@"[DFPointer] pointer lock state -> %d (wanted %d)",
                  scene.pointerLockState.isLocked, wantLock);
        }];

        DFPointerAttachAll();
    }

    // Hover recogniser on Unity's view. iOS 13+, so no availability guard needed at the
    // project's 13.0 floor.
    UIView *view = UnityGetGLView();
    if (view != nil && hoverRecognizer == nil)
    {
        hoverTarget = [[DFPointerHoverTarget alloc] init];
        hoverRecognizer = [[UIHoverGestureRecognizer alloc] initWithTarget:hoverTarget action:@selector(hover:)];
        [view addGestureRecognizer:hoverRecognizer];
    }

    DFPointerInstallLockOverride();

    NSLog(@"[DFPointer] initialised: %d mice, %d keyboards present", attachedMice, attachedKeyboards);
}

/// True while at least one mouse/trackpad is attached. This, not axis movement, is what
/// decides that a pointer is in play.
bool DFPointerConnected()
{
    if (@available(iOS 14.0, *))
        return [GCMouse mice].count > 0;
    return false;
}

/// Raw movement since the last call, then zeroed. Positive Y is UP (GameController
/// convention, matching Unity's Mouse Y).
void DFPointerConsumeDelta(float *dx, float *dy)
{
    if (dx) *dx = accumDX;
    if (dy) *dy = accumDY;
    accumDX = 0.0f;
    accumDY = 0.0f;
}

/// Bitmask: 1 = left, 2 = right, 4 = middle, 8 = any auxiliary button.
int DFPointerButtons()
{
    return buttonMask;
}

/// Scroll accumulated since the last call, then zeroed.
float DFPointerConsumeScroll()
{
    float s = accumScroll;
    accumScroll = 0.0f;
    return s;
}

/// Last hover position, normalised 0..1 with a bottom-left origin. False until the
/// pointer has been seen over the view at least once.
bool DFPointerHover(float *nx, float *ny)
{
    if (nx) *nx = hoverNX;
    if (ny) *ny = hoverNY;
    return hoverValid;
}

void DFPointerSetLocked(bool locked)
{
    if (!initialised)
        DFPointerInit();

    BOOL want = locked ? YES : NO;
    if (want == wantLock && lockOverrideInstalled)
        return;

    wantLock = want;
    DFPointerInstallLockOverride();
    DFPointerRequestLockUpdate();
}

/// The system's answer, not our request.
bool DFPointerIsLocked()
{
    if (@available(iOS 14.0, *))
    {
        UIViewController *vc = UnityGetGLViewController();
        UIScene *scene = vc.view.window.windowScene;
        return scene != nil && scene.pointerLockState != nil && scene.pointerLockState.isLocked;
    }
    return false;
}


/// True while a hardware keyboard is attached (GCKeyboard, iOS 14+).
bool DFKeyboardConnected()
{
    if (@available(iOS 14.0, *))
        return [GCKeyboard coalescedKeyboard] != nil;
    return false;
}

/// Number of keys currently down.
int DFKeyboardHeldCount()
{
    return keysHeld;
}

/// Copies the HID usage codes of every key currently down into codes (up to max) and
/// returns how many were written. Codes are GCKeyCode values = USB HID keyboard usages.
int DFKeyboardSnapshot(int *codes, int max)
{
    int n = 0;
    if (codes == NULL || max <= 0)
        return 0;
    for (int c = 0; c < 256 && n < max; c++)
        if (keyDown[c])
            codes[n++] = c;
    return n;
}
} // extern "C"
