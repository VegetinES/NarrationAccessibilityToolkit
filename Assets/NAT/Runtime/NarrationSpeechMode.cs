namespace NarrationAccessibilityToolkit
{
    // Controls how a new spoken message interacts with speech that may already be playing.
    public enum NarrationSpeechMode
    {
        // Stop current speech and speak the new message immediately.
        Interrupt,

        // Put the message behind any currently active queued announcements.
        Queue
    }
}

