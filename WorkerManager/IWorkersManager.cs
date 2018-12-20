using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WorkerManager
{
    public interface IWorkersManager
    {
        event EventHandler<IWorker> Added;
        event EventHandler<IWorker> Updated;
        event EventHandler<IWorker> Deleted;
        int Count { get; }
        IEnumerable<IWorker>Workers { get; }
        void Load();
        IWorker AddWorker();
        void DeleteWorker(IWorker worker);
        void Close();
        bool DeleteWorker(int port);
        bool KillWorker(int port);
    }
}