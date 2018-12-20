using System;

namespace WorkerManager
{
    public interface IWorker : IDisposable
    {
        int Port { get; }
        string Label { get; }
        void Start();
        void Kill();
        string GetRamUsage();
        string GetCpuLoad();
    }
}