using System.Text;
using Auxil.Common.Text;

namespace Auxil.Extensions;

internal sealed class ContextualSplitter(char[] separators, ContextualStringSplitOptions options)
{
    private readonly StringBuilder _word = new();
    private readonly List<string> _result = [];
    private bool _currentIsDigit;
    private bool _currentIsAcronym;

    public string[] Split(string text)
    {
        foreach (char c in text)
        {
            Consume(c);
        }

        AddWord(_word.ToString());

        return _result.ToArray();
    }

    private void Consume(char c)
    {
        if (separators.Contains(c))
        {
            FlushWord();
            return;
        }

        if (options.IncludeNumbers && ConsumeDigitRun(c))
        {
            return;
        }

        if (options.IncludeAcronyms && ConsumeAcronymBoundary(c))
        {
            return;
        }

        _word.Append(c);
    }

    private bool ConsumeDigitRun(char c)
    {
        if (c.IsDigit())
        {
            if (!_word.IsEmpty && !_currentIsDigit)
            {
                FlushWord();
            }

            _currentIsDigit = true;
            _word.Append(c);
            return true;
        }

        if (_currentIsDigit)
        {
            FlushWord();
            _currentIsDigit = false;
        }

        return false;
    }

    private bool ConsumeAcronymBoundary(char c)
    {
        if (c.IsLetter() && c.IsUpper())
        {
            string lastStringChar = _word.PeekBack();
            string secondLastStringChar = _word.PeekBack(2);

            bool addWordBeforeAcronyms =
                !lastStringChar.IsNullOrEmpty()
                && !secondLastStringChar.IsNullOrEmpty()
                && secondLastStringChar[0].IsLower()
                && lastStringChar[0].IsUpper();

            if (!_word.IsEmpty && addWordBeforeAcronyms)
            {
                AddWord(_word.ToString()[..^1]);
                _word.Clear().Append(lastStringChar[0]).Append(c);
                _currentIsAcronym = true;
                return true;
            }
        }

        if (c.IsLetter() && c.IsLower() && _currentIsAcronym)
        {
            FlushWord();
            _currentIsAcronym = false;
        }

        return false;
    }

    private void FlushWord()
    {
        AddWord(_word.ToString());
        _word.Clear();
    }

    private void AddWord(string word)
    {
        word = options.TrimEntries ? word.Trim() : word;

        if (options.RemoveEmptyEntries && word.IsNullOrEmpty())
        {
            return;
        }

        _result.Add(word);
    }
}
