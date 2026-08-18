// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Real iOS haptics via the Taptic Engine. Unity's Handheld.Vibrate() is useless here:
// on iPad it does nothing at all (no vibration motor), and on iPhone it triggers the
// harsh legacy full-device buzz rather than a crisp tap.
//
// Place in Assets/Plugins/iOS/

#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *lightGen  = nil;
static UIImpactFeedbackGenerator *mediumGen = nil;
static UIImpactFeedbackGenerator *heavyGen  = nil;
static UISelectionFeedbackGenerator *selectionGen = nil;

extern "C" {

/// iPads have no Taptic Engine, so the generators exist but produce nothing.
/// Report that honestly instead of pretending feedback happened.
bool DFMobileHapticsSupported()
{
    if ([[UIDevice currentDevice] userInterfaceIdiom] == UIUserInterfaceIdiomPad)
        return false;

    return NSClassFromString(@"UIImpactFeedbackGenerator") != nil;
}

/// Allocating and warming the generators up front avoids the latency spike on first use.
void DFMobileHapticsPrepare()
{
    if (!DFMobileHapticsSupported())
        return;

    if (lightGen == nil)
    {
        lightGen  = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
        mediumGen = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
        heavyGen  = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
        selectionGen = [[UISelectionFeedbackGenerator alloc] init];
    }

    [lightGen prepare];
    [mediumGen prepare];
    [heavyGen prepare];
    [selectionGen prepare];
}

/// style: 0 = light, 1 = medium, 2 = heavy
void DFMobileHapticsImpact(int style)
{
    if (!DFMobileHapticsSupported())
        return;

    DFMobileHapticsPrepare();

    UIImpactFeedbackGenerator *gen = mediumGen;
    if (style <= 0) gen = lightGen;
    else if (style >= 2) gen = heavyGen;

    [gen impactOccurred];
    [gen prepare];
}

void DFMobileHapticsSelection()
{
    if (!DFMobileHapticsSupported())
        return;

    DFMobileHapticsPrepare();
    [selectionGen selectionChanged];
    [selectionGen prepare];
}

}
