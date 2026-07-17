using PersistLens.Domain;

namespace PersistLens.Application;

public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
