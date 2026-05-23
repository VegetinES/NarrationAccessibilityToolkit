#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>
#import <UIKit/UIKit.h>

static AVSpeechSynthesizer *NarrationAccessibilityToolkitSynthesizer()
{
    static AVSpeechSynthesizer *synthesizer = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        synthesizer = [[AVSpeechSynthesizer alloc] init];
    });
    return synthesizer;
}

extern "C"
{
    void PN_Speak(const char *text, const char *languageCode, bool interrupt)
    {
        if (text == NULL)
        {
            return;
        }

        NSString *message = [NSString stringWithUTF8String:text];
        NSString *language = languageCode == NULL ? @"en-US" : [NSString stringWithUTF8String:languageCode];

        dispatch_async(dispatch_get_main_queue(), ^{
            if (UIAccessibilityIsVoiceOverRunning())
            {
                UIAccessibilityPostNotification(UIAccessibilityAnnouncementNotification, message);
                return;
            }

            AVSpeechSynthesizer *synthesizer = NarrationAccessibilityToolkitSynthesizer();
            if (interrupt)
            {
                [synthesizer stopSpeakingAtBoundary:AVSpeechBoundaryImmediate];
            }

            AVSpeechUtterance *utterance = [AVSpeechUtterance speechUtteranceWithString:message];
            utterance.voice = [AVSpeechSynthesisVoice voiceWithLanguage:language];
            [synthesizer speakUtterance:utterance];
        });
    }

    void PN_Stop()
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            [NarrationAccessibilityToolkitSynthesizer() stopSpeakingAtBoundary:AVSpeechBoundaryImmediate];
        });
    }
}
