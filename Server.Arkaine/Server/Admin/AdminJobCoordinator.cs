namespace Server.Arkaine.Admin
{
    public enum AdminJobKind
    {
        Thumbnails,
        Conversion
    }

    public sealed class AdminJobCoordinator
    {
        private readonly object _syncRoot = new();
        private AdminJobKind? _activeJob;

        public bool TryAcquire(AdminJobKind job)
        {
            lock (_syncRoot)
            {
                if (_activeJob is not null)
                {
                    return false;
                }

                _activeJob = job;
                return true;
            }
        }

        public void Release(AdminJobKind job)
        {
            lock (_syncRoot)
            {
                if (_activeJob == job)
                {
                    _activeJob = null;
                }
            }
        }
    }
}
