using Auxil.Extensions;
using Auxil.Utilities.DummyUtility;
using JetBrains.Annotations;

namespace Auxil.Tests.Utilities;

[TestSubject(typeof(Dummy))]
public class DummyTest
{
    [Fact]
    public void Word_LoremIpsum_ReturnsAWordFromTheLoremList()
    {
        StaticDummyData.LoremWords.ShouldContain(Dummy.Word(DummyDataType.LoremIpsum));
    }

    [Fact]
    public void Word_EnglishGibberish_ReturnsAWordFromTheGibberishList()
    {
        StaticDummyData.RandomEnglishWords.ShouldContain(
            Dummy.Word(DummyDataType.EnglishGibberish)
        );
    }

    [Fact]
    public void Word_ThrowsForInvalidType()
    {
        Should.Throw<ArgumentException>(() => Dummy.Word((DummyDataType)99));
    }

    [Fact]
    public void Words_ReturnsRequestedCount()
    {
        Dummy.Words(DummyDataType.LoremIpsum, 5).Count.ShouldBe(5);
    }

    [Fact]
    public void Sentence_RespectsInclusiveBounds()
    {
        string sentence = Dummy.Sentence(DummyDataType.LoremIpsum, 5, 5);

        sentence.Words.Count.ShouldBe(5);
    }

    [Fact]
    public void Sentence_IsCapitalizedAndEndsWithPeriod()
    {
        string sentence = Dummy.Sentence(DummyDataType.LoremIpsum, 3, 3);

        char.IsUpper(sentence[0]).ShouldBeTrue();
        sentence.ShouldEndWith(".");
    }

    [Fact]
    public void Sentences_ReturnsRequestedCountWithSeparator()
    {
        string sentences = Dummy.Sentences(DummyDataType.LoremIpsum, 4, " | ");

        sentences.Split(" | ").Length.ShouldBe(4);
    }

    [Fact]
    public void Paragraph_RespectsInclusiveBounds()
    {
        string paragraph = Dummy.Paragraph(DummyDataType.LoremIpsum, 2, 2);

        paragraph.CountSentences().ShouldBe(2);
    }

    [Fact]
    public void Paragraph_WithEqualBounds_UsesThatSentenceCount()
    {
        string paragraph = Dummy.Paragraph(DummyDataType.LoremIpsum, 2, 2);

        paragraph.Count(character => character == (char)10).ShouldBe(1);
    }

    [Fact]
    public void Sentence_WithMinimumJustAboveMaximum_UsesTheMinimum()
    {
        string sentence = Dummy.Sentence(DummyDataType.LoremIpsum, 6, 5);

        sentence.Split(' ').Length.ShouldBe(6);
    }

    [Fact]
    public void Sentence_WithMinimumFarAboveMaximum_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            Dummy.Sentence(DummyDataType.LoremIpsum, 9, 5)
        );
    }
}
