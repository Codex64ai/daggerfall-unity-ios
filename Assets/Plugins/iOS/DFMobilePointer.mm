// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// UIKit hover bridge for the iPad Magic Keyboard trackpad. Unity's legacy input
// path can expose a trackpad as an indirect touch only while its button is down;
// UIHoverGestureRecognizer receives the pointer's hover movement independently.

#import <UIKit/UIKit.h>
#import "UnityInterface.h"

static CGPoint pointerPosition = {0, 0};
static CGPoint pointerDelta = {0, 0};
static BOOL pointerActive = NO;
static BOOL pointerHidden = NO;
static BOOL pointerButtonHeld = NO;
static int pointerClickFrames = 0;
static UIHoverGestureRecognizer *hoverRecognizer = nil;
static UITapGestureRecognizer *tapRecognizer = nil;
static UIPointerInteraction *pointerInteraction = nil;

@interface DFMobilePointerDelegate : NSObject <UIPointerInteractionDelegate>
- (void)pointerTap:(UITapGestureRecognizer *)recognizer;
@end

@implementation DFMobilePointerDelegate
- (void)pointerTap:(UITapGestureRecognizer *)recognizer
{
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

static void EnsurePointerBridge()
{
    if (@available(iOS 13.4, *))
    {
        UIView *view = UnityGetGLView();
        if (view == nil)
            return;

        if (hoverRecognizer == nil)
        {
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
    CGPoint location = [recognizer locationInView:self];
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

bool DFMobilePointerRead(float *x, float *y, float *dx, float *dy, bool *buttonHeld)
{
    EnsurePointerBridge();
    if (!pointerActive)
        return false;

    *x = (float)pointerPosition.x;
    *y = (float)pointerPosition.y;
    *dx = (float)pointerDelta.x;
    *dy = (float)pointerDelta.y;
    *buttonHeld = pointerButtonHeld || pointerClickFrames > 0;
    if (pointerClickFrames > 0)
        pointerClickFrames--;
    pointerDelta = CGPointZero;
    return true;
}

void DFMobilePointerSetHidden(bool hidden)
{
    EnsurePointerBridge();
    pointerHidden = hidden;
    if (@available(iOS 13.4, *) && pointerInteraction != nil)
        [pointerInteraction invalidate];
}

}
