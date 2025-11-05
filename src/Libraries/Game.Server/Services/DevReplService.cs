using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuantumCore.API.Game.World;
using QuantumCore.Game.World.Entities;

namespace QuantumCore.Game.Services;

public class DevReplService(ILogger<DevReplService> logger, IHostEnvironment env, IWorld world)
    : BackgroundService
{
    private TcpListener? _listener;
    private const int Port = 19100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!env.IsDevelopment())
        {
            logger.LogDebug("Dev REPL disabled");
            return;
        }

        try
        {
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();
            logger.LogInformation("Dev REPL listening on 127.0.0.1:{Port}", Port);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dev REPL failed to bind to 127.0.0.1:{Port}. Continuing without REPL.", Port);
            return;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(stoppingToken);
                _ = HandleClientAsync(client, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "REPL accept loop failed");
        }
        finally
        {
            try { _listener.Stop(); }
            catch
            {
                /* ignore */
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var _ = client;
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, new UTF8Encoding(false));
        var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

        await writer.WriteLineAsync("QuantumCore Dev REPL ready. Globals: GameServer.Instance, World, Admin");
        await writer.WriteLineAsync("Type C# statements/expressions. 'exit' to close.");
        await writer.WriteAsync(">> ");

        ScriptState<object>? state = null;
        var options = BuildScriptOptions();
        var globals = new Globals(world);

        while (!ct.IsCancellationRequested && await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                await writer.WriteAsync(">> ");
                continue;
            }

            if (line.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) break;
            try
            {
                if (state == null)
                    state = await CSharpScript.RunAsync(line, options, globals, typeof(Globals), ct);
                else
                    state = await state.ContinueWithAsync(line, options, ct);

                if (state?.ReturnValue is not null)
                {
                    await writer.WriteLineAsync(state.ReturnValue.ToString());
                }
            }
            catch (CompilationErrorException cee)
            {
                foreach (var d in cee.Diagnostics)
                    await writer.WriteLineAsync(d.ToString());
            }
            catch (Exception e)
            {
                await writer.WriteLineAsync($"Error: {e.GetType().Name}: {e.Message}");
            }

            await writer.WriteAsync(">> ");
        }
    }

    private static ScriptOptions BuildScriptOptions()
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Distinct();

        return ScriptOptions.Default
            .AddReferences(refs)
            .AddImports("System",
                "System.Linq",
                "System.Collections.Generic",
                "QuantumCore.API",
                "QuantumCore.API.Game",
                "QuantumCore.API.Game.World",
                "QuantumCore.API.Game.Types",
                "QuantumCore.Game",
                "QuantumCore.Game.Packets");
    }

    public sealed class Globals(IWorld world)
    {
        public IWorld World { get; } = world;

        public PlayerEntity Admin => World.GetPlayer("Admin")! as PlayerEntity ?? throw new InvalidOperationException();

        // private object Scratchpad()
        // {
        //     return Admin!.
        // }
    }
}
