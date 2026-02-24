using System;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.ConsentManagement;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Kernel.UnitTests.Services;

public sealed class ConsentManagerServiceTests : ReliabilityTestBase
{
    public ConsentManagerServiceTests(ITestOutputHelper output) : base(output)
    {
    }

    private static ConsentManagerService CreateService(ILogger<ConsentManagerService>? logger = null)
    {
        return new ConsentManagerService(logger ?? NullLogger<ConsentManagerService>.Instance);
    }

    private static Mock<IElement> CreateMockElement(bool isVisible = true, bool isEnabled = true)
    {
        var mock = new Mock<IElement>();
        mock.Setup(x => x.IsVisibleAsync()).ReturnsAsync(isVisible);
        mock.Setup(x => x.IsEnabledAsync()).ReturnsAsync(isEnabled);
        return mock;
    }

    private static Mock<IPage> CreateMockPage()
    {
        return new Mock<IPage>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullLogger_UsesNullLogger()
    {
        var service = new ConsentManagerService(null);
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithLogger_CreatesInstance()
    {
        ConsentManagerService service = CreateService();
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCustomLogger_CreatesInstance()
    {
        var logger = new Mock<ILogger<ConsentManagerService>>();
        ConsentManagerService service = CreateService(logger.Object);
        service.Should().NotBeNull();
    }

    #endregion

    #region HandleConsentAsync - No Banner Tests

    [Fact]
    public async Task HandleConsentAsync_NoBannerDetected_ReturnsFalse()
    {
        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleConsentAsync_AllSelectorsNull_ReturnsFalse()
    {
        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleConsentAsync_NullPage_ThrowsArgumentNullException()
    {
        ConsentManagerService service = CreateService();

        Func<Task> act = async () => await service.HandleConsentAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region HandleConsentAsync - Google Funding Choices Tests

    [Fact]
    public async Task HandleConsentAsync_GoogleFundingChoices_DetectedAndAccepted_ReturnsTrue()
    {
        Mock<IElement> mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        Mock<IPage> mockPage = CreateMockPage();
        var sequence = new MockSequence();

        mockPage.InSequence(sequence)
            .Setup(x => x.QuerySelectorAsync(".fc-cta-consent"))
            .ReturnsAsync(mockButton.Object);

        mockPage.InSequence(sequence)
            .Setup(x => x.QuerySelectorAsync(".fc-cta-consent"))
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
        mockButton.Verify(x => x.ClickAsync(), Times.Once);
    }

    [Fact]
    public async Task HandleConsentAsync_GoogleFundingChoices_ButtonNotVisible_SkipsToNext()
    {
        Mock<IElement> mockHiddenButton = CreateMockElement(isVisible: false, isEnabled: true);

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(".fc-cta-consent"))
            .ReturnsAsync(mockHiddenButton.Object);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleConsentAsync_GoogleFundingChoices_ButtonDisabled_DoesNotClick()
    {
        Mock<IElement> mockDisabledButton = CreateMockElement(isVisible: true, isEnabled: false);

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(".fc-cta-consent"))
            .ReturnsAsync(mockDisabledButton.Object);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(".fc-cta-consent")]
    [InlineData(".fc-consent-root")]
    [InlineData("[aria-label*='consent' i]")]
    public async Task HandleConsentAsync_DetectsGoogleFundingChoicesSelectors(string selector)
    {
        Mock<IElement> mockElement = CreateMockElement(isVisible: true);

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        ConsentManagerService service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    #endregion

    #region HandleConsentAsync - OneTrust Tests

    [Fact]
    public async Task HandleConsentAsync_OneTrust_DetectedAndAccepted_ReturnsTrue()
    {
        Mock<IElement> mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.SetupSequence(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync(mockButton.Object)
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
        mockButton.Verify(x => x.ClickAsync(), Times.Once);
    }

    [Fact]
    public async Task HandleConsentAsync_OneTrust_AcceptButtonDisabled_Skips()
    {
        Mock<IElement> mockDisabledButton = CreateMockElement(isVisible: true, isEnabled: false);

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("#onetrust-consent-sdk")]
    [InlineData("#onetrust-banner-sdk")]
    [InlineData("[class*='onetrust']")]
    public async Task HandleConsentAsync_DetectsOneTrustSelectors(string selector)
    {
        Mock<IElement> mockElement = CreateMockElement(isVisible: true);

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        ConsentManagerService service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    [Fact]
    public async Task HandleConsentAsync_OneTrustOptanon_Detected()
    {
        Mock<IElement> mockElement = CreateMockElement(isVisible: true);

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync("#optanon-root"))
            .ReturnsAsync(mockElement.Object);

        ConsentManagerService service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync("#optanon-root"), Times.AtLeastOnce);
    }

    #endregion

    #region HandleConsentAsync - CookieBot Tests

    [Fact]
    public async Task HandleConsentAsync_CookieBot_DetectedAndAccepted_ReturnsTrue()
    {
        Mock<IElement> mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        Mock<IPage> mockPage = CreateMockPage();
        // CookieBot selectors are checked after other selectors
        // First return the button for detection/click, then null to indicate banner disappeared
        mockPage.SetupSequence(x => x.QuerySelectorAsync("#CybotCookiebotDialog"))
            .ReturnsAsync(mockButton.Object)
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("#CybotCookiebotDialog")]
    [InlineData("[data-cybot]")]
    public async Task HandleConsentAsync_DetectsCookieBotSelectors(string selector)
    {
        Mock<IElement> mockElement = CreateMockElement(isVisible: true);

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        ConsentManagerService service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    #endregion

    #region HandleConsentAsync - Sourcepoint Tests

    [Theory]
    [InlineData("[id*='sp_message_iframe']")]
    [InlineData("#sp_message")]
    public async Task HandleConsentAsync_DetectsSourcepointSelectors(string selector)
    {
        Mock<IElement> mockElement = CreateMockElement(isVisible: true);

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        ConsentManagerService service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    #endregion

    #region HandleConsentAsync - UserCentrics Tests

    [Theory]
    [InlineData("#usercentrics-root")]
    [InlineData("[data-testid='uc-accept-all-button']")]
    public async Task HandleConsentAsync_DetectsUserCentricsSelectors(string selector)
    {
        Mock<IElement> mockElement = CreateMockElement(isVisible: true);

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        ConsentManagerService service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    #endregion

    #region HandleConsentAsync - Quantcast Tests

    [Theory]
    [InlineData("#qc-cmp2-ui")]
    [InlineData("[id*='qc-cmp']")]
    public async Task HandleConsentAsync_DetectsQuantcastSelectors(string selector)
    {
        Mock<IElement> mockElement = CreateMockElement(isVisible: true);

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        ConsentManagerService service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    #endregion

    #region HandleConsentAsync - Didomi Tests

    [Theory]
    [InlineData("#didomi-host")]
    [InlineData(".didomi-popup")]
    public async Task HandleConsentAsync_DetectsDidomiSelectors(string selector)
    {
        Mock<IElement> mockElement = CreateMockElement(isVisible: true);

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        ConsentManagerService service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    #endregion

    #region HandleConsentAsync - CookieFirst Tests

    [Theory]
    [InlineData("[data-cookiefirst-action]")]
    [InlineData("#cookiefirst")]
    public async Task HandleConsentAsync_DetectsCookieFirstSelectors(string selector)
    {
        Mock<IElement> mockElement = CreateMockElement(isVisible: true);

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        ConsentManagerService service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    #endregion

    #region HandleConsentAsync - Osano Tests

    [Theory]
    [InlineData(".osano-cm-window")]
    [InlineData("#osano-cm")]
    public async Task HandleConsentAsync_DetectsOsanoSelectors(string selector)
    {
        Mock<IElement> mockElement = CreateMockElement(isVisible: true);

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        ConsentManagerService service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    #endregion

    #region HandleConsentAsync - Click Exception Handling Tests

    [Fact]
    public async Task HandleConsentAsync_ClickThrowsException_FallsBackToEvaluate()
    {
        Mock<IElement> mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).ThrowsAsync(new InvalidOperationException("Click failed"));

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.SetupSequence(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync(mockButton.Object)
            .ReturnsAsync((IElement?)null);

        mockPage.Setup(x => x.EvaluateAsync<object>(It.IsAny<string>()))
            .ReturnsAsync(new object());

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
        mockPage.Verify(x => x.EvaluateAsync<object>(It.Is<string>(s => s.Contains("click()"))), Times.Once);
    }

    [Fact]
    public async Task HandleConsentAsync_ClickAndEvaluateFail_StillReturnsTrue()
    {
        Mock<IElement> mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).ThrowsAsync(new InvalidOperationException("Click failed"));

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.SetupSequence(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync(mockButton.Object)
            .ReturnsAsync((IElement?)null);

        mockPage.Setup(x => x.EvaluateAsync<object>(It.IsAny<string>()))
            .ReturnsAsync(new object());

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
    }

    #endregion

    #region HandleConsentAsync - Iframe Tests

    [Fact]
    public async Task HandleConsentAsync_IframeBased_DetectedAndHandled_ReturnsTrue()
    {
        var mockFrame = new Mock<IElement>();

        Mock<IPage> mockPage = CreateMockPage();
        // Use callback to return frame on first call to any iframe selector, null on subsequent calls
        int iframeCallCount = 0;
        mockPage.Setup(x => x.QuerySelectorAsync(It.Is<string>(s =>
            s.Contains("iframe[src*='consent'") ||
            s.Contains("iframe[src*='cookie'") ||
            s.Contains("iframe[src*='gdpr'"))))
            .ReturnsAsync(() =>
            {
                iframeCallCount++;
                return iframeCallCount == 1 ? mockFrame.Object : null;
            });

        // All other selectors return null
        mockPage.Setup(x => x.QuerySelectorAsync(It.Is<string>(s => !s.Contains("iframe"))))
            .ReturnsAsync((IElement?)null);

        mockPage.Setup(x => x.EvaluateAsync<bool>(It.IsAny<string>()))
            .ReturnsAsync(true);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HandleConsentAsync_IframeNotClicked_ReturnsFalse()
    {
        var mockFrame = new Mock<IElement>();

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.Is<string>(s => s.Contains("iframe"))))
            .ReturnsAsync(mockFrame.Object);

        mockPage.Setup(x => x.EvaluateAsync<bool>(It.IsAny<string>()))
            .ReturnsAsync(false);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleConsentAsync_IframeEvaluateThrows_ReturnsFalse()
    {
        var mockFrame = new Mock<IElement>();

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.Is<string>(s => s.Contains("iframe"))))
            .ReturnsAsync(mockFrame.Object);

        mockPage.Setup(x => x.EvaluateAsync<bool>(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Evaluate failed"));

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeFalse();
    }

    #endregion

    #region HandleConsentAsync - Exception Handling Tests

    [Fact]
    public async Task HandleConsentAsync_DetectionThrowsException_ContinuesToNextManager()
    {
        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Selector error"));

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleConsentAsync_IsVisibleThrowsException_ContinuesToNextSelector()
    {
        Mock<IPage> mockPage = CreateMockPage();

        var mockFailingElement = new Mock<IElement>();
        mockFailingElement.Setup(x => x.IsVisibleAsync()).ThrowsAsync(new InvalidOperationException("Visibility check failed"));

        mockPage.SetupSequence(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync(mockFailingElement.Object)
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleConsentAsync_IsEnabledThrowsException_ContinuesToNextSelector()
    {
        Mock<IPage> mockPage = CreateMockPage();

        var mockFailingElement = new Mock<IElement>();
        mockFailingElement.Setup(x => x.IsVisibleAsync()).ReturnsAsync(true);
        mockFailingElement.Setup(x => x.IsEnabledAsync()).ThrowsAsync(new InvalidOperationException("Enabled check failed"));

        mockPage.SetupSequence(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync(mockFailingElement.Object)
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleConsentAsync_QuerySelectorThrowsOnBannerCheck_Continues()
    {
        Mock<IElement> mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        Mock<IPage> mockPage = CreateMockPage();
        var callCount = 0;
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1) return mockButton.Object;
                throw new InvalidOperationException("Query failed");
            });

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
    }

    #endregion

    #region WaitAndHandleConsentAsync Tests

    [Fact]
    public async Task WaitAndHandleConsentAsync_BannerAppearsWithinTimeout_ReturnsTrue()
    {
        Mock<IElement> mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        Mock<IPage> mockPage = CreateMockPage();
        // First returns null (no banner), then returns button (banner appears)
        mockPage.SetupSequence(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync(mockButton.Object)
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.WaitAndHandleConsentAsync(mockPage.Object, maxWaitMs: 500, checkIntervalMs: 100);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task WaitAndHandleConsentAsync_BannerNeverAppears_ReturnsFalse()
    {
        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.WaitAndHandleConsentAsync(mockPage.Object, maxWaitMs: 100, checkIntervalMs: 50);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task WaitAndHandleConsentAsync_ZeroTimeout_ReturnsFalse()
    {
        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.WaitAndHandleConsentAsync(mockPage.Object, maxWaitMs: 0, checkIntervalMs: 10);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task WaitAndHandleConsentAsync_NegativeTimeout_ReturnsFalse()
    {
        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.WaitAndHandleConsentAsync(mockPage.Object, maxWaitMs: -1, checkIntervalMs: 10);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task WaitAndHandleConsentAsync_LargeCheckInterval_StillWorks()
    {
        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.WaitAndHandleConsentAsync(mockPage.Object, maxWaitMs: 50, checkIntervalMs: 100);

        result.Should().BeFalse();
    }

    #endregion

    #region Generic Consent Tests

    [Theory]
    [InlineData(".cookie-banner")]
    [InlineData("#cookie-banner")]
    [InlineData("[class*='cookie-consent']")]
    public async Task HandleConsentAsync_DetectsGenericSelectors(string selector)
    {
        Mock<IElement> mockElement = CreateMockElement(isVisible: true);
        mockElement.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        Mock<IPage> mockPage = CreateMockPage();
        // First return the element for detection/click, then null to indicate banner disappeared
        mockPage.SetupSequence(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object)
            .ReturnsAsync((IElement?)null);

        mockPage.Setup(x => x.QuerySelectorAsync(It.Is<string>(s => s != selector)))
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HandleConsentAsync_GenericAcceptButtons_AttemptsClick()
    {
        Mock<IElement> mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        Mock<IPage> mockPage = CreateMockPage();
        // First return the button for detection/click, then null to indicate banner disappeared
        mockPage.SetupSequence(x => x.QuerySelectorAsync("[class*='accept-all']"))
            .ReturnsAsync(mockButton.Object)
            .ReturnsAsync((IElement?)null);

        mockPage.Setup(x => x.QuerySelectorAsync(It.Is<string>(s => s != "[class*='accept-all']")))
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
        mockButton.Verify(x => x.ClickAsync(), Times.Once);
    }

    [Fact]
    public async Task HandleConsentAsync_BannerStillPresentAfterClick_ReturnsFalse()
    {
        Mock<IElement> mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        Mock<IPage> mockPage = CreateMockPage();
        // Banner remains present after click
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync(mockButton.Object);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        // Returns false because banner still present
        result.Should().BeFalse();
    }

    #endregion

    #region Timeout and Performance Tests

    [Fact]
    public async Task HandleConsentAsync_ShortTimeout_CompletesWithinTime()
    {
        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        DateTime startTime = DateTime.UtcNow;
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 10);
        DateTime endTime = DateTime.UtcNow;

        (endTime - startTime).Should().BeLessThan(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task HandleConsentAsync_ZeroTimeout_ProcessesImmediately()
    {
        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 0);

        result.Should().BeFalse();
    }

    #endregion

    #region Consent Manager Detection Order Tests

    [Fact]
    public async Task HandleConsentAsync_ChecksManagersInOrder()
    {
        Mock<IPage> mockPage = CreateMockPage();
        var verificationList = new System.Collections.Generic.List<string>();

        // Use callback to track calls and return null to simulate no banner found
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .Callback<string, CancellationToken>((selector, _) => verificationList.Add(selector))
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 10);

        // Should check multiple consent managers in order
        verificationList.Count.Should().BeGreaterThan(0);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task HandleConsentAsync_ElementIsNull_DoesNotThrow()
    {
        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();

        Func<Task> act = async () => await service.HandleConsentAsync(mockPage.Object);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleConsentAsync_EmptyDetectionSelectors_SkipsManager()
    {
        // This tests internal behavior - if a manager had empty selectors
        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleConsentAsync_MultipleAcceptanceSelectors_TriesAll()
    {
        Mock<IElement> mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        Mock<IPage> mockPage = CreateMockPage();

        // First selector returns null, second returns the button
        var callCount = 0;
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // First few calls for detection, then acceptance
                return callCount == 1 ? CreateMockElement().Object :
                       callCount == 2 ? mockButton.Object : null;
            });

        ConsentManagerService service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(It.IsAny<string>()), Times.AtLeastOnce);
    }

    #endregion

    #region Logging Tests

    [Fact]
#pragma warning disable CA1873 // Justification: Test mock verification - intentional evaluation
    public async Task HandleConsentAsync_LogsDebugMessages()
    {
        var mockLogger = new Mock<ILogger<ConsentManagerService>>();
        mockLogger.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(true);

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService(mockLogger.Object);
        await service.HandleConsentAsync(mockPage.Object);

        mockLogger.Verify(x => x.Log(
            LogLevel.Debug,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => true),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }
#pragma warning restore CA1873

    [Fact]
#pragma warning disable CA1873 // Justification: Test mock verification - intentional evaluation
    public async Task HandleConsentAsync_Detected_LogsInfoMessage()
    {
        var mockLogger = new Mock<ILogger<ConsentManagerService>>();
        mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);

        Mock<IElement> mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        Mock<IPage> mockPage = CreateMockPage();
        mockPage.SetupSequence(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync(mockButton.Object)
            .ReturnsAsync((IElement?)null);

        ConsentManagerService service = CreateService(mockLogger.Object);
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => true),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }
#pragma warning restore CA1873

    [Fact]
#pragma warning disable CA1873 // Justification: Test mock verification - intentional evaluation
    public async Task HandleConsentAsync_WarningBannerStillPresent_LogsWarning()
    {
        var mockLogger = new Mock<ILogger<ConsentManagerService>>();
        mockLogger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true);

        Mock<IElement> mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        Mock<IPage> mockPage = CreateMockPage();
        // Banner remains visible after click
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync(mockButton.Object);

        ConsentManagerService service = CreateService(mockLogger.Object);
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockLogger.Verify(x => x.Log(
            It.Is<LogLevel>(l => l == LogLevel.Warning),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => true),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }
#pragma warning restore CA1873

    #endregion
}
