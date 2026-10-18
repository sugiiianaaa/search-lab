using Lucene.Net.Analysis;

namespace SearchLab.Search.Lucene;

/// <summary>Lucene only ships LowerCaseFilter, which runs after the tokenizer: too late for the tech term mapping.</summary>
internal sealed class LowerCaseCharFilter(TextReader input) : CharFilter(input)
{
    public override int Read(char[] buffer, int index, int count)
    {
        int read = m_input.Read(buffer, index, count);

        for (int i = index; i < index + read; i++)
        {
            buffer[i] = char.ToLowerInvariant(buffer[i]);
        }

        return read;
    }

    protected override int Correct(int currentOff) => currentOff;
}
