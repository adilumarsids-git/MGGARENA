using UnityEngine;

namespace ProjectA.Core
{
    public enum ProjectALogLevel
    {
        Info,
        Warn,
        Error
    }

    public static class ProjectALogger
    {
        private const string Prefix = "[ProjectA]";

        public static void Log(ProjectALogLevel level, string message, Object context = null)
        {
            var formatted = $"{Prefix} [{level}] {message}";

            switch (level)
            {
                case ProjectALogLevel.Info:
                    Debug.Log(formatted, context);
                    break;
                case ProjectALogLevel.Warn:
                    Debug.LogWarning(formatted, context);
                    break;
                case ProjectALogLevel.Error:
                    Debug.LogError(formatted, context);
                    break;
            }
        }

        public static void Info(string message, Object context = null) => Log(ProjectALogLevel.Info, message, context);
        public static void Warn(string message, Object context = null) => Log(ProjectALogLevel.Warn, message, context);
        public static void Error(string message, Object context = null) => Log(ProjectALogLevel.Error, message, context);
    }
}
