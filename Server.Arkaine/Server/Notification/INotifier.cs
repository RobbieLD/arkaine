namespace Server.Arkaine.Notification
{
    public interface INotifier
    {
        public Task Send(string message);
    }
}
