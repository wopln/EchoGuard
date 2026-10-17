using System;

namespace EchoGuard
{
    public static class ChangePolicy
    {
        public static bool NotificationMatchesCurrent(bool notificationMute, bool currentMute)
        {
            return notificationMute == currentMute;
        }

        public static bool ShouldRestore(Change record, bool currentMute)
        {
            return record != null && !record.ExternalOverride && currentMute && !record.OriginalMute;
        }
    }
}
