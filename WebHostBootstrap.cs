using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SpireX.AspNetCore.Boot
{
    public class WebHostBootstrap
    {
        private readonly IHost _host;
        private readonly ICollection<Bootable> _bootables;
        private readonly ILogger _logger;
        private readonly object _locker = new object();

        private readonly IList<Bootable> _executableList = new List<Bootable>();
        private readonly IList<Bootable> _waitingList = new List<Bootable>();
        private readonly IList<BootKey> _completed = new List<BootKey>();

        private WebHostBootstrap(IHost host, ICollection<Bootable> bootables, ILogger logger)
        {
            _host = host;
            _bootables = bootables;
            _logger = logger;
        }

        public void Boot()
        {
            BootProcess().Wait();
            _host.Run();
        }

        private async Task BootProcess()
        {
            BuildExecutableQueue();
            while (_executableList.Count > 0)
            {
                await Start();
                CheckWaitingList();
            }
        }

        private void BuildExecutableQueue()
        {
            foreach (var bootable in _bootables)
            {
                if (bootable.Dependencies.Length > 0)
                {
                    var found = false;
                    foreach (var dependencyKey in bootable.Dependencies)
                    {
                        foreach (var other in _bootables)
                        {
                            if (other.Key == dependencyKey)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                            throw new Exception("Cant resolve dependency");
                    }
                    _waitingList.Add(bootable);
                }
                else
                    _executableList.Add(bootable);
            }
        }

        private void CheckWaitingList()
        {
            foreach (var bootable in _waitingList.ToList())
            {
                if (CheckBootableCanRun(bootable))
                {
                    _waitingList.Remove(bootable);
                    _executableList.Add(bootable);
                }
            }
        }

        private bool CheckBootableCanRun(Bootable bootable) =>
            bootable.Dependencies.All(key => _completed.Contains(key));

        private Task Start()
        {
            IList<Task> bootableTasks = new List<Task>();
            foreach (var bootable in _executableList)
            {
                var bootableTask = Task.Run(() =>
                {
                    try
                    {
                        var watcher = System.Diagnostics.Stopwatch.StartNew();
                        _logger?.LogInformation("Boot run: {key}", bootable.Key.Name);
                        bootable.Boot();
                        watcher.Stop();
                        lock (_locker)
                        {
                            _completed.Add(bootable.Key);
                        }
                        _logger?.LogInformation("Boot success: {key} ({time} ms)", bootable.Key.Name, watcher.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError("Boot failed: {key}\n{error}", ex.Message, ex.StackTrace);
                        if (bootable.IsCritical)
                            throw new Exception("Не выполнен критический объект");
                    }
                });
                bootableTasks.Add(bootableTask);
            }
            _executableList.Clear();
            return Task.WhenAll(bootableTasks);
        }

        public static WebHostBootstrapBuilder ForHost(IHost host) =>
            new WebHostBootstrapBuilder(host);

        public class WebHostBootstrapBuilder
        {
            private readonly IList<Bootable> _bootables = new List<Bootable>();
            private readonly IHost _host;
            private ILogger _logger;

            internal WebHostBootstrapBuilder(IHost host)
            {
                _host = host;
            }

            public WebHostBootstrapBuilder UseBootable<TBootable>() where TBootable : Bootable
            {
                var bootable = ActivatorUtilities.GetServiceOrCreateInstance<TBootable>(_host.Services);
                if (bootable != null)
                    _bootables.Add(bootable);
                return this;
            }

            public WebHostBootstrapBuilder UseLogger(ILogger logger)
            {
                _logger = logger;
                return this;
            }

            public WebHostBootstrap Create() => new WebHostBootstrap(_host, _bootables, _logger);
        }
    }
}