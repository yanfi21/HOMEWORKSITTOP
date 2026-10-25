namespace Shop.Web.Logging;

/// <summary>
/// Собственный провайдер встроенной системы логирования ASP.NET Core (ILogger / ILoggerProvider).
/// Пишет логи в файл logs/app-yyyyMMdd.log без сторонних библиотек (Serilog/NLog не нужны).
/// Уровни и фильтры настраиваются стандартной секцией "Logging" в appsettings.json.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly object _lock = new();

    public FileLoggerProvider(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    public void Dispose()
    {
    }

    internal void Write(string line)
    {
        var path = Path.Combine(_logDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
        lock (_lock)
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly FileLoggerProvider _provider;

        public FileLogger(string category, FileLoggerProvider provider)
        {
            _category = category;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {_category}: {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            try
            {
                _provider.Write(line);
            }
            catch
            {
                // Логирование никогда не должно ронять приложение.
            }
        }
    }
}
