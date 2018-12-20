using System;
using System.Windows.Forms;

namespace WorkerManager
{
    public interface IWorkersManager
    {
        event EventHandler<IWorker> Added;
        event EventHandler<IWorker> Deleted;
        int Count { get; }
        void Load();
        IWorker AddWorker();
        void DeleteWorker(IWorker worker);
        void Close();
        bool DeleteWorker(int port);
    }
}