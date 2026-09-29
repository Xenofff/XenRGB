using System;
using System.Threading;

namespace XenRGB.App;

public static class Program
{
    private const string AppMutexName = @"Local\XenRGB_SingleInstance_Mutex";
    private static Mutex? _mutex;

    [STAThread]
    public static void Main(string[] args)
    {
        bool createdNew;
        try
        {
            _mutex = new Mutex(true, AppMutexName, out createdNew);
        }
        catch
        {
            // If mutex creation fails, do not proceed
            return;
        }

        if (!createdNew)
        {
            // Another instance is already running; exit immediately without starting UI, OpenRGB, or background services.
            _mutex.Dispose();
            _mutex = null;
            return;
        }

        try
        {
            XamlGeneratedProgram.XamlGeneratedMain();
        }
        finally
        {
            if (_mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Ignored if mutex is not owned or was already released
                }
                _mutex.Dispose();
                _mutex = null;
            }
        }
    }
}
