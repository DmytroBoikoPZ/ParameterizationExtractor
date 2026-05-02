using Microsoft.Extensions.Logging;
using Quipu.ParameterizationExtractor.Common;
using Quipu.ParameterizationExtractor.Logic.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Quipu.ParameterizationExtractor
{
    public class FromFileExecutor : IExecutor
    {
        private readonly ILogger _log;
        private readonly IAppArgs _args;
        private readonly PackageProcessor _packageProcessor;
        private readonly ICanSerializeConfigs _config;

        public FromFileExecutor(ILogger<FromFileExecutor> log, IAppArgs args, PackageProcessor packageProcessor, ICanSerializeConfigs config)
        {
            Affirm.ArgumentNotNull(packageProcessor, "packageProcessor");
            Affirm.ArgumentNotNull(log, "log");
            Affirm.ArgumentNotNull(args, "args");
            Affirm.ArgumentNotNull(config, "config");

            _packageProcessor = packageProcessor;
            _log = log;
            _args = args;
            _config = config;
        }

        public async Task Execute(CancellationToken token)
        {
            var pckg = _config.GetPackage(_args.PathToPackage);

            await _packageProcessor.ExecuteAsync(token, pckg);
        }
    }
}
