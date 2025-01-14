namespace LBPUnion.PLRPC.Logging;

public static class Message
{
    public static void New(int type, string message)
    {
        string Log = type switch
        {
            0 => $"<PLRPC> {DateTime.Now} [INFO] {message}",
            1 => $"<PLRPC> {DateTime.Now} [NOTICE] {message}",
            2 => $"<PLRPC> {DateTime.Now} [WARN] {message}",
            3 => $"<PLRPC> {DateTime.Now} [ERROR] {message}",
            _ => $"<PLRPC> {DateTime.Now} {message}"
        };
        Console.WriteLine(Log);
    }

    public static void Exception(string exception)
    {
        New(3, "");
        New(3, "*** PLRPC has experienced an error and will now exit. ***");
        New(3, "This is most likely *not your fault*. Try restarting the client and check your configuration.");
        New(3, "If this error persists, please create a new GitHub issue using the Bug Report template.");
        New(3, "");
        New(3, $"{exception}");
        New(3, "");
    }
}
