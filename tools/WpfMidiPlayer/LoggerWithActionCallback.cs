using System;

namespace WpfMidiPlayer
{
  public class LoggerWithActionCallback : ILogger
  {
    private readonly Action<string> _loggerAction;

    public LoggerWithActionCallback(Action<string> loggerAction)
    {
      _loggerAction = loggerAction ?? throw new ArgumentNullException(nameof(loggerAction));
    }

    public void Debug(string message, params object[] args)
    {
      Log("DEBUG: ", message, args);
    }

    public void Trace(string message, params object[] args)
    {
      Log("TRACE: ", message, args);
    }

    public void Info(string message, params object[] args)
    {
      Log("INFO: ", message, args);
    }

    public void Warn(string message, params object[] args)
    {
      Log("Warn: ", message, args);
    }

    public void Error(string message, params object[] args)
    {
      Log("ERROR: ", message, args);
    }

    public void Fatal(string message, params object[] args)
    {
      Log("FATAL: ", message, args);
    }

    private void Log(string prefix, string message, object[] args)
    {
      _loggerAction.Invoke(string.Format(prefix + message, args));
    }
  }
}