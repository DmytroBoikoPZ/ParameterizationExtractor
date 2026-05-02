using Microsoft.Extensions.Logging;
using ParameterizationExtractor.DSL.Connector;
using Quipu.ParameterizationExtractor.Common;
using Quipu.ParameterizationExtractor.DSL;
using Quipu.ParameterizationExtractor.Logic.Interfaces;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Quipu.ParameterizationExtractor.DSL.AST;

namespace Quipu.ParameterizationExtractor
{
    public class DSLExecutor : IExecutor
    {
        private const int READLINE_BUFFER_SIZE = 1024;

        private readonly ILogger _log;
        private readonly IAppArgs _args;
        private readonly PackageProcessor _packageProcessor;
        private readonly IDSLConnector _dslConnector;
        private readonly ISourceSchema _sourceSchema;
        public DSLExecutor(ILogger<DSLExecutor> log, IAppArgs args, PackageProcessor packageProcessor, IDSLConnector dslConnector, ISourceSchema sourceSchema)
        {
            Affirm.ArgumentNotNull(packageProcessor, "packageProcessor");
            Affirm.ArgumentNotNull(log, "log");
            Affirm.ArgumentNotNull(args, "args");
            Affirm.ArgumentNotNull(dslConnector, "dslConnector");
            Affirm.ArgumentNotNull(sourceSchema, "sourceSchema");

            _packageProcessor = packageProcessor;
            _log = log;
            _args = args;
            _dslConnector = dslConnector;
            _sourceSchema = sourceSchema;
        }

        private static string ReadLine()
        {
            Stream inputStream = Console.OpenStandardInput(READLINE_BUFFER_SIZE);
            byte[] bytes = new byte[READLINE_BUFFER_SIZE];
            int outputLength = inputStream.Read(bytes, 0, READLINE_BUFFER_SIZE);
            char[] chars = Encoding.UTF7.GetChars(bytes, 0, outputLength);
            return new string(chars); 
        }

        public async Task Execute(CancellationToken token)
        {
            _log.Debug("Interactive mode");
            AST.Command cmd = null;

            ColoredWriteLine("Type #exit to exit, #help to get help", ConsoleColor.Green);

            do
            {
                Console.Write('>');
                var input = ReadLine() ?? string.Empty;

                if (input.StartsWith("#"))
                    cmd = await ExecInternalCommand(input, token);
                else if (!string.IsNullOrEmpty(input))
                    await ExecDSLInput(input, token);
            }
            while (cmd != Command.Exit && !token.IsCancellationRequested);
        }

        private async Task<AST.Command> ExecInternalCommand(string input, CancellationToken token)
        {
            try
            {
                var parseResult = InternalCommandParser.parse(input);

                if (parseResult is ParserResult.CommandOK command)
                {
                    if (command.GetResult == AST.Command.Help)
                    {
                        ColoredWriteLine("there is no help for you. Check source code.", ConsoleColor.Red);
                    }
                    else if (command.GetResult is AST.Command.CheckTable checkTable)
                    {
                        foreach (var i in _sourceSchema.Tables.Where(_ => _.TableName.ToLower().Contains(checkTable.Item.ToLower())).ToList())
                            ColoredWriteLine(i.TableName, ConsoleColor.White);
                    }
                    else if (command.GetResult is AST.Command.GetMetadata getMetadata)
                    {
                        var item = _sourceSchema.Tables.FirstOrDefault(_ => _.TableName.Equals(getMetadata.Item, StringComparison.InvariantCultureIgnoreCase));
                        if (item != null)
                        {
                            foreach (var i in item)
                                ColoredWriteLine("{0} {1}".FormIt(i.FieldName, i.BaseTypeName), ConsoleColor.White);
                        }
                        else
                            ConsoleWarning("Table was not found in DB schema");
                    }
                    else if (command.GetResult is AST.Command.ChangeSource changeSource)
                    {
                        _args.ConnectionName = changeSource.Item;

                        await _sourceSchema.Init(token);
                    }

                    return await Task.FromResult<Command>(command.GetResult);
                }

                ConsoleWarning((parseResult as ParserResult.Fail)?.ErrorMessage);
                return null;
            }
            catch(Exception ex)
            {
                _log.Error(ex);
                return null;
            }
        }

        private async Task ExecDSLInput(string input, CancellationToken token)
        {
            try
            {
                var package = _dslConnector.Parse(input);
                _log.Debug("input is OK");

                await _packageProcessor.ExecuteAsync(token, package);
            }
            catch (DSLParseException e)
            {
                ConsoleWarning(e.Message);
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
        }

        private void ColoredWriteLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);

            Console.ResetColor();
        }

        private void ConsoleWarning(string text)
        {
            ColoredWriteLine(text, ConsoleColor.Yellow);
        }
    }
}
