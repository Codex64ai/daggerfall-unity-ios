// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// UIKit hover bridge for the iPad Magic Keyboard trackpad. Unity's legacy input
// path can expose a trackpad as an indirect touch only while its button is down;
// UIHoverGestureRecognizer receives the pointer's hover movement independently.

#import <UIKit/UIKit.h>
#import <GameController/GameController.h>
#import <objc/runtime.h>
#import "UnityInterface.h"

static CGPoint pointerPosition = {0, 0};
static CGPoint pointerDelta = {0, 0};
static BOOL pointerActive = NO;
static BOOL pointerHidden = NO;
static BOOL pointerLockRequested = NO;
static BOOL directTouchActive = NO;
static BOOL pointerButtonHeld = NO;
static BOOL pointerSecondaryButtonHeld = NO;
static BOOL pointerAtEdge = NO;
static int pointerClickFrames = 0;
static UIHoverGestureRecognizer *hoverRecognizer = nil;
static UITapGestureRecognizer *tapRecognizer = nil;
static UIPointerInteraction *pointerInteraction = nil;
static IMP originalWindowSendEvent = nil;
static NSUInteger diagnosticWindowEvents = 0;
static NSUInteger diagnosticIndirectTouches = 0;
static NSUInteger diagnosticNonZeroDeltas = 0;
static NSUInteger diagnosticHoverEvents = 0;
static UIEventType diagnosticLastEventType = UIEventTypeTouches;
static NSUInteger diagnosticGameControllerDeltas = 0;
static GCMouse *gameControllerMouse = nil;
static id pointerLockObserver = nil;
static Class pointerLockPreferenceClass = nil;
static BOOL pointerLockOverrideOff = NO;
static BOOL pointerLockRecoveryScheduled = NO;
static NSTimeInterval pointerLockRecoveryTime = 0;
static NSUInteger diagnosticLockRecoveries = 0;
static NSUInteger diagnosticDirectTouches = 0;
static NSUInteger diagnosticStyleRequests = 0;
static NSUInteger diagnosticUnlocksWhileHeld = 0;
static NSUInteger diagnosticUnlocksWhileIdle = 0;
static BOOL nativeDirectTouchActive = NO;
static BOOL windowSizeLocked = NO;
static BOOL windowSizeLockApplied = NO;

// UIKit drops the lock the moment it needs the pointer for something of its own
// (an edge affordance, a system gesture). Retry no faster than this so a scene
// that legitimately cannot lock - Split View, backgrounded - is not hammered.
static const NSTimeInterval pointerLockRecoveryInterval = 0.5;

@interface DFMobilePointerDelegate : NSObject <UIPointerInteractionDelegate>
- (void)pointerTap:(UITapGestureRecognizer *)recognizer;
@end

@implementation DFMobilePointerDelegate
- (UIPointerRegion *)pointerInteraction:(UIPointerInteraction *)interaction
                     regionForRequest:(UIPointerRegionRequest *)request
{
    if (@available(iOS 13.4, *))
        return [UIPointerRegion regionWithRect:interaction.view.bounds identifier:nil];
    return nil;
}

- (void)pointerTap:(UITapGestureRecognizer *)recognizer
{
    if (pointerHidden)
        return;

    // UIKit delivers some Magic Keyboard clicks as a tap without exposing a
    // press state to Unity. Keep the synthetic button down for two frames so
    // the classic UI observes a normal down/up edge after the tap completes.
    pointerClickFrames = 2;
    pointerActive = YES;
}

- (UIPointerStyle *)pointerInteraction:(UIPointerInteraction *)interaction
                      styleForRegion:(UIPointerRegion *)region
{
    if (@available(iOS 13.4, *))
    {
        diagnosticStyleRequests++;
        return pointerHidden ? [UIPointerStyle hiddenPointerStyle] : nil;
    }
    return nil;
}
@end

static DFMobilePointerDelegate *pointerDelegate = nil;

// The pointer counts as "on the edge" only while UIKit is actually drawing it
// there. Recomputed from every position sample rather than latched, because the
// hover recognizer that used to be its only source is disabled during gameplay -
// a latched value would outlive the press that set it.
static void DFMobilePointerUpdateEdge(CGPoint location, CGSize bounds)
{
    const CGFloat edgeInset = 2.0;
    pointerAtEdge = location.x <= edgeInset || location.y <= edgeInset ||
                    location.x >= bounds.width - edgeInset ||
                    location.y >= bounds.height - edgeInset;
}

static void DFMobilePointerRecordEvent(UIEvent *event, UIView *view)
{
    if (!pointerLockRequested || event == nil || view == nil)
        return;

    for (UITouch *touch in event.allTouches)
    {
        if (touch.type != UITouchTypeIndirectPointer)
            continue;

        diagnosticIndirectTouches++;

        CGPoint current = [touch locationInView:view];
        CGPoint previous = [touch previousLocationInView:view];
        CGFloat scale = view.window.screen.scale;
        pointerDelta.x += (current.x - previous.x) * scale;
        pointerDelta.y -= (current.y - previous.y) * scale;
        if (current.x != previous.x || current.y != previous.y)
            diagnosticNonZeroDeltas++;
        pointerPosition = CGPointMake(current.x * scale,
                                      (view.bounds.size.height - current.y) * scale);
        DFMobilePointerUpdateEdge(current, view.bounds.size);
        pointerActive = YES;
    }
}

// Which touches are really fingers on the glass.
//
// Unity cannot answer this. Its TouchType has only Direct/Indirect/Stylus, and
// UITouchTypeIndirectPointer - the Magic Keyboard trackpad's own click, added in
// iOS 13.4 - falls through its mapping to Direct. So every press of the trackpad
// button looked to the C# layer like a finger on the screen, and the mute that is
// meant to stop a finger fighting the trackpad fired on the trackpad itself, for
// the whole of every weapon swing. Classify here, where the real UITouch type is
// visible.
static void DFMobilePointerClassifyTouches(UIEvent *event)
{
    if (event == nil)
        return;
    if (event.type != UIEventTypeTouches)
        return;

    BOOL directTouch = NO;
    for (UITouch *touch in event.allTouches)
    {
        if (touch.type != UITouchTypeDirect)
            continue;
        if (touch.phase == UITouchPhaseEnded || touch.phase == UITouchPhaseCancelled)
            continue;

        directTouch = YES;
        diagnosticDirectTouches++;
        break;
    }

    nativeDirectTouchActive = directTouch;
}

static void DFMobilePointerWindowSendEvent(UIWindow *window, SEL selector, UIEvent *event)
{
    diagnosticWindowEvents++;
    diagnosticLastEventType = event.type;
    DFMobilePointerClassifyTouches(event);
    DFMobilePointerRecordEvent(event, UnityGetGLView());
    ((void (*)(id, SEL, UIEvent *))originalWindowSendEvent)(window, selector, event);
}

static void InstallWindowEventCapture()
{
    if (originalWindowSendEvent != nil)
        return;

    Method method = class_getInstanceMethod([UIWindow class], @selector(sendEvent:));
    if (method == nil)
        return;

    originalWindowSendEvent = method_getImplementation(method);
    method_setImplementation(method, (IMP)DFMobilePointerWindowSendEvent);
}

static void InstallGameControllerMouse()
{
    if (@available(iOS 14.0, *))
    {
        GCMouse *currentMouse = GCMouse.current;
        if (currentMouse == nil || currentMouse == gameControllerMouse)
            return;

        gameControllerMouse = currentMouse;
        GCMouseInput *mouseInput = gameControllerMouse.mouseInput;
        if (mouseInput != nil)
        {
            mouseInput.mouseMovedHandler = ^(GCMouseInput *mouse, float deltaX, float deltaY) {
                (void)mouse;
                // Gate on the value C# hands back, not on nativeDirectTouchActive
                // directly: the classification happens here but the watchdog that
                // releases a touch iPadOS abandoned lives up there, and gating on the
                // raw flag would put the mute beyond its reach.
                if (!pointerLockRequested || directTouchActive)
                    return;

                pointerDelta.x += deltaX;
                pointerDelta.y += deltaY;
                pointerActive = YES;
                diagnosticGameControllerDeltas++;
            };

            // The button state has to come from here. While UIKit holds the pointer
            // locked the indirect touch stream stops carrying position changes, and
            // Unity's Input System never sees the Magic Keyboard trackpad at all -
            // pointerButtonHeld was left permanently NO, so every read reported an
            // unpressed pointer for the whole of a swing.
            mouseInput.leftButton.pressedChangedHandler =
                ^(GCControllerButtonInput *button, float value, BOOL pressed) {
                    (void)button;
                    (void)value;
                    pointerButtonHeld = pressed;
                };

            GCControllerButtonInput *rightButton = mouseInput.rightButton;
            if (rightButton != nil)
            {
                rightButton.pressedChangedHandler =
                    ^(GCControllerButtonInput *button, float value, BOOL pressed) {
                        (void)button;
                        (void)value;
                        pointerSecondaryButtonHeld = pressed;
                    };
            }
        }
    }
}

static BOOL DFMobilePointerIsLocked()
{
    if (@available(iOS 14.0, *))
    {
        UIWindowScene *scene = UnityGetGLView().window.windowScene;
        return scene.pointerLockState != nil && scene.pointerLockState.locked;
    }
    return NO;
}

static void UpdatePointerLockPreference()
{
    if (@available(iOS 14.0, *))
        [UnityGetGLView().window.rootViewController setNeedsUpdateOfPrefersPointerLocked];
}

// Reacquire a lock UIKit has taken away from us.
//
// Asking again for a preference UIKit already holds does nothing: it knows we
// prefer the pointer locked and dropped the lock deliberately, so
// setNeedsUpdateOfPrefersPointerLocked re-reads the same YES and stops there.
// Only a NO -> YES transition makes it lock again, which is why the camera used
// to come back only after a trip into a menu and out - the one thing in the game
// that toggled the preference. Do that transition here instead.
static void RecoverPointerLock()
{
    if (@available(iOS 14.0, *))
    {
        if (!pointerLockRequested || pointerLockRecoveryScheduled)
            return;
        if ([UIApplication sharedApplication].applicationState != UIApplicationStateActive)
            return;
        if (DFMobilePointerIsLocked())
            return;

        NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];
        if (now - pointerLockRecoveryTime < pointerLockRecoveryInterval)
            return;

        pointerLockRecoveryTime = now;
        pointerLockRecoveryScheduled = YES;
        diagnosticLockRecoveries++;

        pointerLockOverrideOff = YES;
        UpdatePointerLockPreference();
        dispatch_async(dispatch_get_main_queue(), ^{
            pointerLockOverrideOff = NO;
            UpdatePointerLockPreference();
            pointerLockRecoveryScheduled = NO;
        });
    }
}

static BOOL DFMobilePointerPrefersLocked(id object, SEL selector)
{
    (void)object;
    (void)selector;
    return pointerLockRequested && !pointerLockOverrideOff;
}

static void InstallPointerLockPreference(UIView *view)
{
    if (view == nil)
        return;

    UIViewController *controller = view.window.rootViewController;
    if (controller == nil)
        return;

    if (@available(iOS 14.0, *))
    {
        // Pointer locking is a preference, so ask UIKit to reevaluate it whenever
        // gameplay enters or leaves its pointer-owned mode.
    }
    else
    {
        return;
    }

    Class controllerClass = [controller class];
    if (pointerLockPreferenceClass == controllerClass)
        return;

    SEL selector = @selector(prefersPointerLocked);
    if (!class_addMethod(controllerClass, selector, (IMP)DFMobilePointerPrefersLocked, "c@:"))
    {
        Method method = class_getInstanceMethod(controllerClass, selector);
        if (method != nil)
            method_setImplementation(method, (IMP)DFMobilePointerPrefersLocked);
    }

    pointerLockPreferenceClass = controllerClass;
    [controller setNeedsUpdateOfPrefersPointerLocked];
}

static void EnsurePointerBridge()
{
    if (@available(iOS 13.4, *))
    {
        UIView *view = UnityGetGLView();
        if (view == nil)
            return;

        if (hoverRecognizer == nil)
        {
            InstallWindowEventCapture();
            InstallGameControllerMouse();
            hoverRecognizer = [[UIHoverGestureRecognizer alloc] initWithTarget:view
                                                                           action:@selector(df_mobilePointerHover:)];
            [view addGestureRecognizer:hoverRecognizer];

            if (@available(iOS 14.0, *))
            {
                if (pointerLockObserver == nil)
                {
                    // UIKit announces the moment it takes the pointer back, which is
                    // the moment to ask for it again.
                    pointerLockObserver = [[NSNotificationCenter defaultCenter]
                        addObserverForName:UIPointerLockStateDidChangeNotification
                                    object:nil
                                     queue:[NSOperationQueue mainQueue]
                                usingBlock:^(NSNotification *notification) {
                                    (void)notification;
                                    if (pointerLockRequested && !DFMobilePointerIsLocked())
                                    {
                                        if (pointerButtonHeld || pointerSecondaryButtonHeld)
                                            diagnosticUnlocksWhileHeld++;
                                        else
                                            diagnosticUnlocksWhileIdle++;
                                    }
                                    // Re-resolve the cursor for the mode we are in
                                    // before asking for the lock back, so the gap
                                    // between the two is not a visible pointer.
                                    if (pointerInteraction != nil)
                                        [pointerInteraction invalidate];
                                    RecoverPointerLock();
                                }];
                }
            }
        }

        if (pointerDelegate == nil)
        {
            pointerDelegate = [[DFMobilePointerDelegate alloc] init];
            pointerInteraction = [[UIPointerInteraction alloc] initWithDelegate:pointerDelegate];
            pointerInteraction.enabled = YES;
            [view addInteraction:pointerInteraction];

            tapRecognizer = [[UITapGestureRecognizer alloc]
                initWithTarget:pointerDelegate action:@selector(pointerTap:)];
            tapRecognizer.numberOfTapsRequired = 1;
            tapRecognizer.cancelsTouchesInView = NO;
            if (@available(iOS 13.4, *))
                tapRecognizer.allowedTouchTypes = @[@(UITouchTypeIndirectPointer)];
            [view addGestureRecognizer:tapRecognizer];
        }
    }
}

@interface UIView (DFMobilePointerPrivate)
- (void)df_mobilePointerHover:(UIHoverGestureRecognizer *)recognizer;
@end

@implementation UIView (DFMobilePointerPrivate)
- (void)df_mobilePointerHover:(UIHoverGestureRecognizer *)recognizer
{
    diagnosticHoverEvents++;
    CGPoint location = [recognizer locationInView:self];
    DFMobilePointerUpdateEdge(location, self.bounds.size);
    CGFloat scale = self.window.screen.scale;
    CGPoint normalized = CGPointMake(location.x * scale,
                                     (self.bounds.size.height - location.y) * scale);
    pointerDelta = CGPointMake(normalized.x - pointerPosition.x,
                               normalized.y - pointerPosition.y);
    pointerPosition = normalized;
    pointerActive = recognizer.state != UIGestureRecognizerStateEnded &&
                    recognizer.state != UIGestureRecognizerStateCancelled;
}
@end

extern "C" {

bool DFMobilePointerRead(float *x, float *y, float *dx, float *dy, bool *buttonHeld, bool *atEdge,
                         bool *secondaryButtonHeld, bool *directTouch)
{
    EnsurePointerBridge();
    *secondaryButtonHeld = pointerSecondaryButtonHeld;
    *directTouch = nativeDirectTouchActive;
    if (!pointerActive)
        return false;

    *x = (float)pointerPosition.x;
    *y = (float)pointerPosition.y;
    *dx = (float)pointerDelta.x;
    *dy = (float)pointerDelta.y;
    *buttonHeld = pointerButtonHeld || pointerClickFrames > 0;
    // A locked pointer has no screen position to be on the edge of, and a held
    // button is a swing in progress. Reporting an edge in either case suppressed
    // the swing button for as long as the state stayed stale, which - with the
    // hover recognizer disabled during gameplay - was forever.
    *atEdge = pointerAtEdge && !pointerButtonHeld && !pointerSecondaryButtonHeld &&
              !DFMobilePointerIsLocked();
    if (pointerClickFrames > 0)
        pointerClickFrames--;
    pointerDelta = CGPointZero;
    return true;
}

void DFMobilePointerDiagnostics(unsigned int *windowEvents, unsigned int *indirectTouches,
                                unsigned int *nonZeroDeltas, unsigned int *hoverEvents,
                                unsigned int *gameControllerDeltas, int *lastEventType, bool *locked,
                                unsigned int *lockRecoveries, unsigned int *directTouches,
                                unsigned int *styleRequests, unsigned int *unlocksWhileHeld,
                                unsigned int *unlocksWhileIdle)
{
    *lockRecoveries = (unsigned int)diagnosticLockRecoveries;
    *directTouches = (unsigned int)diagnosticDirectTouches;
    *styleRequests = (unsigned int)diagnosticStyleRequests;
    *unlocksWhileHeld = (unsigned int)diagnosticUnlocksWhileHeld;
    *unlocksWhileIdle = (unsigned int)diagnosticUnlocksWhileIdle;
    *windowEvents = (unsigned int)diagnosticWindowEvents;
    *indirectTouches = (unsigned int)diagnosticIndirectTouches;
    *nonZeroDeltas = (unsigned int)diagnosticNonZeroDeltas;
    *hoverEvents = (unsigned int)diagnosticHoverEvents;
    *lastEventType = (int)diagnosticLastEventType;
    *gameControllerDeltas = (unsigned int)diagnosticGameControllerDeltas;
    *locked = DFMobilePointerIsLocked();
}

void DFMobilePointerSetHidden(bool hidden)
{
    EnsurePointerBridge();

    BOOL changed = pointerLockRequested != (BOOL)hidden;
    if (changed)
        pointerDelta = CGPointZero;
    pointerHidden = hidden;
    pointerLockRequested = hidden;

    InstallPointerLockPreference(UnityGetGLView());
    // GCMouse.current can remain nil until the trackpad is first actuated. Retry
    // here on every mode transition/poll without replacing an existing handler.
    InstallGameControllerMouse();

    // This runs once per frame, so everything below is transition-only. Asking
    // UIKit to reevaluate the lock or invalidating the pointer interaction every
    // frame makes it re-resolve the cursor continuously, which is visible as the
    // system pointer flickering back in over the game.
    if (changed)
    {
        UpdatePointerLockPreference();

        // A hover recognizer consumes the absolute, screen-space pointer stream.
        // That stream is intentionally stationary once UIKit locks the pointer, and
        // keeping the recognizer enabled prevents Unity's Input System from
        // receiving the relative HID events that games need. Let Unity own the
        // locked stream; restore the recognizer for menu hit-testing.
        if (hoverRecognizer != nil)
            hoverRecognizer.enabled = !hidden;

        // The tap recognizer asks UIKit for the click as a located touch, which
        // UIKit can only produce by releasing the pointer lock. It is also inert
        // during gameplay - pointerTap: returns immediately while the pointer is
        // hidden - so all it did there was cost the lock on every weapon swing.
        // Menus keep it; gameplay takes its buttons from GCMouse instead.
        if (tapRecognizer != nil)
            tapRecognizer.enabled = !hidden;

        if (@available(iOS 13.4, *))
        {
            // The interaction stays installed for both modes and the delegate picks
            // the style, so the hidden style still applies when iPadOS drops the
            // lock mid-swing. Disabling it here used to hand the cursor straight
            // back to the system the moment the trackpad button went down.
            if (pointerInteraction != nil)
                [pointerInteraction invalidate];
        }
    }
    else if (hidden)
    {
        RecoverPointerLock();
    }
}

void DFMobilePointerSetDirectTouchActive(bool active)
{
    directTouchActive = active;
    if (active)
        pointerDelta = CGPointZero;
}

void DFMobilePointerLockWindowSize(bool locked)
{
    // Transition-only. This runs once a frame, and rewriting sizeRestrictions on
    // every one of them asks UIKit to re-resolve the scene geometry continuously -
    // and a scene whose geometry is in flux is one UIKit will not hold a pointer
    // lock for.
    if (windowSizeLockApplied && windowSizeLocked == (BOOL)locked)
        return;

    UIView *view = UnityGetGLView();
    if (view == nil)
        return;

    UIWindowScene *scene = view.window.windowScene;
    if (scene == nil)
        return;

    if (@available(iOS 13.0, *))
    {
        if (locked)
        {
            CGSize size = view.bounds.size;
            scene.sizeRestrictions.minimumSize = size;
            scene.sizeRestrictions.maximumSize = size;
        }
        else
        {
            scene.sizeRestrictions.minimumSize = CGSizeZero;
            scene.sizeRestrictions.maximumSize = CGSizeZero;
        }

        windowSizeLocked = locked;
        windowSizeLockApplied = YES;
    }
}

}
