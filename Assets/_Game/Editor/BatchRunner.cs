using System;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public static class BatchRunner
    {
        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        public static void Run(Action work)
        {
            try
            {
                work();
                EditorApplication.Exit(SuccessExitCode);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(FailureExitCode);
            }
        }
    }
}
