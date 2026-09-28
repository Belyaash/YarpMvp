namespace YarpMvp;

//При получении Warning по одинаковым URL - кидает ошибку. В продакшн решении надо либо делать форку либо подменять файл через либу harmony.
public sealed class PathConflictToExceptionLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) 
        => new PathConflictLogger(categoryName);

    public void Dispose() { }

    private sealed class PathConflictLogger(string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) 
            => logLevel == LogLevel.Warning; // Перехватываем только Warning

        public void Log<TState>(
            LogLevel logLevel, 
            EventId eventId, 
            TState state, 
            Exception? exception, 
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel != LogLevel.Warning) return;

            var message = formatter(state, exception);

            // Ищем характерный фрагмент сообщения
            if (message.Contains("conflicts with an existing path; keeping the first occurrence.", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"[OpenApiForYarp] Обнаружен конфликт путей, который должен быть устранён. " +
                    $"Исходное сообщение: {message}", 
                    exception);
            }
        }
    }
}