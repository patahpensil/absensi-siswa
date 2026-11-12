using System.Text;

namespace Absensi.App.Gerbang;

internal sealed class SerialUidFrames
{
    private const int MinimumLength = 8;
    private const int MaximumLength = 64;
    private readonly StringBuilder _current = new(MaximumLength);
    private bool _invalid;

    public void Read(string chunk, Action<string> accept)
    {
        foreach (var character in chunk)
        {
            if (character is '\r' or '\n')
            {
                if (!_invalid && _current.Length >= MinimumLength)
                    accept(_current.ToString());
                _current.Clear();
                _invalid = false;
            }
            else if (!_invalid)
            {
                if (_current.Length == MaximumLength || !char.IsAsciiLetterOrDigit(character))
                    _invalid = true;
                else
                    _current.Append(character);
            }
        }
    }
}
