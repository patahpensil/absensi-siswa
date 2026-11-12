using System.IO;
using System.IO.Ports;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Absensi.App.Gerbang;

internal sealed partial class SerialGateReaders : IDisposable
{
    private sealed record Configuration(string[] Ports, int BaudRate = 9600);
    private readonly List<SerialPort> _ports = [];

    public static SerialGateReaders? Open(string configurationPath, Action<string> accept, Action reportFault)
    {
        if (!File.Exists(configurationPath)) return null;
        var configuration = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(configurationPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (configuration?.Ports is not { Length: >= 1 and <= 4 } ports ||
            configuration.BaudRate is < 1200 or > 115200 ||
            ports.Any(port => string.IsNullOrWhiteSpace(port) || !PortName().IsMatch(port)) ||
            ports.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ports.Length)
        {
            throw new InvalidOperationException("Konfigurasi reader harus berisi 1–4 port COM berbeda dan baud rate yang valid.");
        }

        var readers = new SerialGateReaders();
        try
        {
            foreach (var name in ports)
                readers.OpenPort(name.ToUpperInvariant(), configuration.BaudRate, accept, reportFault);
            return readers;
        }
        catch
        {
            readers.Dispose();
            throw;
        }
    }

    private void OpenPort(string name, int baudRate, Action<string> accept, Action reportFault)
    {
        var port = new SerialPort(name, baudRate, Parity.None, 8, StopBits.One);
        var frames = new SerialUidFrames();
        _ports.Add(port);
        port.DataReceived += (_, _) =>
        {
            try { frames.Read(port.ReadExisting(), accept); }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException) { reportFault(); }
        };
        port.ErrorReceived += (_, _) => reportFault();
        port.Open();
    }

    public bool AllConnected()
    {
        var available = SerialPort.GetPortNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _ports.All(port => port.IsOpen && available.Contains(port.PortName));
    }

    public void Dispose()
    {
        foreach (var port in _ports)
        {
            try { port.Dispose(); }
            catch (IOException) { }
        }
        _ports.Clear();
    }

    [GeneratedRegex(@"^COM[1-9][0-9]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PortName();
}
