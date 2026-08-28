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
        return pointerHidden ? [UIPointerStyle hiddenPointerStyle] : nil;
    return nil;
}
@end

static DFMobilePointerDelegate *pointerDelegate = nil;

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
        pointerActive = YES;
    }
}

static void DFMobilePointerWindowSendEvent(UIWindow *window, SEL selector, UIEvent *event)
{
    diagnosticWindowEvents++;
    diagnosticLastEventType = event.type;
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
        if (currentMouse == nil)
            return;

        gameControllerMouse = currentMouse;
        GCMouseInput *mouseInput = gameControllerMouse.mouseInput;
        if (mouseInput != nil)
        {
            mouseInput.mouseMovedHandler = ^(GCMouseInput *mouse, float deltaX, float deltaY) {
                (void)mouse;
                if (!pointerLockRequested || directTouchActive)
                    return;

                pointerDelta.x += deltaX;
                pointerDelta.y += deltaY;
                pointerActive = YES;
                diagnosticGameControllerDeltas++;
            };

        }
    }
}

static BOOL DFMobilePointerPrefersLocked(id object, SEL selector)
{
    (void)object;
    (void)selector;
    return pointerLockRequested;
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
    SEL selector = @selector(prefersPointerLocked);
    if (!class_addMethod(controllerClass, selector, (IMP)DFMobilePointerPrefersLocked, "c@:"))
    {
        Method method = class_getInstanceMethod(controllerClass, selector);
        if (method != nil)
            method_setImplementation(method, (IMP)DFMobilePointerPrefersLocked);
    }
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
        }

        if (pointerDelegate == nil)
        {
            pointerDelegate = [[DFMobilePointerDelegate alloc] init];
            pointerInteraction = [[UIPointerInteraction alloc] initWithDelegate:pointerDelegate];
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
    const CGFloat edgeInset = 2.0;
    pointerAtEdge = location.x <= edgeInset || location.y <= edgeInset ||
                    location.x >= self.bounds.size.width - edgeInset ||
                    location.y >= self.bounds.size.height - edgeInset;
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

bool DFMobilePointerRead(float *x, float *y, float *dx, float *dy, bool *buttonHeld, bool *atEdge)
{
    EnsurePointerBridge();
    if (!pointerActive)
        return false;

    *x = (float)pointerPosition.x;
    *y = (float)pointerPosition.y;
    *dx = (float)pointerDelta.x;
    *dy = (float)pointerDelta.y;
    *buttonHeld = pointerButtonHeld || pointerClickFrames > 0;
    *atEdge = pointerAtEdge;
    if (pointerClickFrames > 0)
        pointerClickFrames--;
    pointerDelta = CGPointZero;
    return true;
}

void DFMobilePointerDiagnostics(unsigned int *windowEvents, unsigned int *indirectTouches,
                                unsigned int *nonZeroDeltas, unsigned int *hoverEvents,
                                unsigned int *gameControllerDeltas, int *lastEventType, bool *locked)
{
    *windowEvents = (unsigned int)diagnosticWindowEvents;
    *indirectTouches = (unsigned int)diagnosticIndirectTouches;
    *nonZeroDeltas = (unsigned int)diagnosticNonZeroDeltas;
    *hoverEvents = (unsigned int)diagnosticHoverEvents;
    *lastEventType = (int)diagnosticLastEventType;
    *gameControllerDeltas = (unsigned int)diagnosticGameControllerDeltas;
    UIView *view = UnityGetGLView();
    UIWindowScene *scene = view.window.windowScene;
    *locked = scene.pointerLockState != nil && scene.pointerLockState.locked;
}

void DFMobilePointerSetHidden(bool hidden)
{
    EnsurePointerBridge();
    if (pointerLockRequested != hidden)
        pointerDelta = CGPointZero;
    pointerHidden = hidden;
    pointerLockRequested = hidden;
    InstallPointerLockPreference(UnityGetGLView());
    // GCMouse.current can remain nil until the trackpad is first actuated. Retry
    // here on every mode transition/poll without replacing an existing handler.
    InstallGameControllerMouse();
    // A hover recognizer consumes the absolute, screen-space pointer stream. That
    // stream is intentionally stationary once UIKit locks the pointer, and keeping
    // the recognizer enabled prevents Unity's Input System from receiving the
    // relative HID events that games need. Let Unity own the locked stream; restore
    // the recognizer for menu hit-testing.
    if (hoverRecognizer != nil)
        hoverRecognizer.enabled = !hidden;
    if (@available(iOS 13.4, *) && pointerInteraction != nil)
    {
        pointerInteraction.enabled = !hidden;
        [pointerInteraction invalidate];
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
    UIView *view = UnityGetGLView();
    UIWindowScene *scene = view.window.windowScene;
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
    }
}

}
