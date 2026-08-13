using CompanyCopilot.Domain.Entities;
using CompanyCopilot.Domain.Enums;
using CompanyCopilot.Domain.Rules;

namespace CompanyCopilot.UnitTests;

public class DocumentRulesTests
{
    private static KnowledgeDocument Document(Action<KnowledgeDocument>? configure = null)
    {
        var document = new KnowledgeDocument { Status = DocumentStatus.Draft };
        configure?.Invoke(document);
        return document;
    }

    [Fact]
    public void IsAuthoritativeCategory_Rules_ReturnsTrue()
    {
        Assert.True(DocumentRules.IsAuthoritativeCategory(DocumentCategory.Rules));
        Assert.True(DocumentRules.IsAuthoritativeCategory(DocumentCategory.PaymentConditions));
        Assert.True(DocumentRules.IsAuthoritativeCategory(DocumentCategory.Hours));
        Assert.True(DocumentRules.IsAuthoritativeCategory(DocumentCategory.Policy));
    }

    [Fact]
    public void IsAuthoritativeCategory_FaqAndInstitutional_ReturnsFalse()
    {
        Assert.False(DocumentRules.IsAuthoritativeCategory(DocumentCategory.Faq));
        Assert.False(DocumentRules.IsAuthoritativeCategory(DocumentCategory.Institutional));
        Assert.False(DocumentRules.IsAuthoritativeCategory(DocumentCategory.Other));
    }

    [Fact]
    public void RequiresValidityPeriod_AuthoritativePayment_RequiresValidFrom()
    {
        var document = Document(d =>
        {
            d.Priority = DocumentPriority.Authoritative;
            d.Category = DocumentCategory.PaymentConditions;
        });

        Assert.True(DocumentRules.RequiresValidityPeriod(document));
    }

    [Fact]
    public void RequiresValidityPeriod_StandardDocument_DoesNotRequire()
    {
        var document = Document(d =>
        {
            d.Priority = DocumentPriority.Standard;
            d.Category = DocumentCategory.PaymentConditions;
        });

        Assert.False(DocumentRules.RequiresValidityPeriod(document));
    }

    [Fact]
    public void CanApprove_AuthoritativeWithoutValidFrom_ReturnsFalse()
    {
        var document = Document(d =>
        {
            d.Priority = DocumentPriority.Authoritative;
            d.Category = DocumentCategory.Hours;
        });

        Assert.False(DocumentRules.CanApprove(document));
    }

    [Fact]
    public void CanApprove_AuthoritativeWithValidFrom_ReturnsTrue()
    {
        var document = Document(d =>
        {
            d.Priority = DocumentPriority.Authoritative;
            d.Category = DocumentCategory.Hours;
            d.ValidFrom = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        });

        Assert.True(DocumentRules.CanApprove(document));
    }

    [Fact]
    public void CanApprove_AlreadyApproved_ReturnsFalse()
    {
        var document = Document(d =>
        {
            d.Status = DocumentStatus.Approved;
            d.ValidFrom = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        });

        Assert.False(DocumentRules.CanApprove(document));
    }

    [Fact]
    public void IsRetrievable_InternalDocument_ReturnsFalse()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var document = Document(d =>
        {
            d.Status = DocumentStatus.Approved;
            d.Visibility = Visibility.Internal;
        });

        Assert.False(DocumentRules.IsRetrievable(document, today));
    }

    [Fact]
    public void IsRetrievable_NotApproved_ReturnsFalse()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var document = Document(d => d.Status = DocumentStatus.Draft);

        Assert.False(DocumentRules.IsRetrievable(document, today));
    }

    [Fact]
    public void IsRetrievable_Archived_ReturnsFalse()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var document = Document(d =>
        {
            d.Status = DocumentStatus.Archived;
            d.Visibility = Visibility.Public;
        });

        Assert.False(DocumentRules.IsRetrievable(document, today));
    }

    [Fact]
    public void IsRetrievable_FutureValidFrom_ReturnsFalse()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var document = Document(d =>
        {
            d.Status = DocumentStatus.Approved;
            d.Visibility = Visibility.Public;
            d.ValidFrom = today.AddDays(2);
        });

        Assert.False(DocumentRules.IsRetrievable(document, today));
    }

    [Fact]
    public void IsRetrievable_ExpiredValidUntil_ReturnsFalse()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var document = Document(d =>
        {
            d.Status = DocumentStatus.Approved;
            d.Visibility = Visibility.Public;
            d.ValidUntil = today.AddDays(-1);
        });

        Assert.False(DocumentRules.IsRetrievable(document, today));
    }

    [Fact]
    public void IsRetrievable_ApprovedPublicAndCurrent_ReturnsTrue()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var document = Document(d =>
        {
            d.Status = DocumentStatus.Approved;
            d.Visibility = Visibility.Public;
            d.ValidFrom = today.AddDays(-30);
            d.ValidUntil = today.AddDays(30);
        });

        Assert.True(DocumentRules.IsRetrievable(document, today));
    }

    [Theory]
    [InlineData(DocumentStatus.Draft, DocumentStatus.Processing, true)]
    [InlineData(DocumentStatus.Failed, DocumentStatus.Processing, true)]
    [InlineData(DocumentStatus.Draft, DocumentStatus.Approved, true)]
    [InlineData(DocumentStatus.Processing, DocumentStatus.Approved, true)]
    [InlineData(DocumentStatus.Rejected, DocumentStatus.Approved, true)]
    [InlineData(DocumentStatus.Approved, DocumentStatus.Archived, true)]
    [InlineData(DocumentStatus.Archived, DocumentStatus.Rejected, true)]
    [InlineData(DocumentStatus.Approved, DocumentStatus.Draft, false)]
    [InlineData(DocumentStatus.Archived, DocumentStatus.Approved, false)]
    [InlineData(DocumentStatus.Rejected, DocumentStatus.Processing, false)]
    [InlineData(DocumentStatus.Failed, DocumentStatus.Approved, false)]
    public void CanTransition_Matrix_MatchesExpected(
        DocumentStatus current, DocumentStatus target, bool expected)
    {
        Assert.Equal(expected, DocumentRules.CanTransition(current, target));
    }
}
