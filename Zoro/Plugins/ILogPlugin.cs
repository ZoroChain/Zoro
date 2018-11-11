namespace Zoro.Plugins
{
    public interface ILogPlugin
    {
        void Log(string source, LogLevel level, string message, UInt160 chainHash);
    }
}
