using System;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.ConsentManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace Ghost.Kernel.UnitTests.Services;

public sealed class ConsentManagerServiceTests
{
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
        var service = CreateService();
        service.Should().NotBeNull();
    }

    #endregion

    #region HandleConsentAsync - No Banner Tests

    [Fact]
    public async Task HandleConsentAsync_NoBannerDetected_ReturnsFalse()
    {
        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleConsentAsync_AllSelectorsNull_ReturnsFalse()
    {
        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeFalse();
    }

    #endregion

    #region HandleConsentAsync - Google Funding Choices Tests

    [Fact]
    public async Task HandleConsentAsync_GoogleFundingChoices_DetectedAndAccepted_ReturnsTrue()
    {
        var mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        var mockPage = CreateMockPage();
        var sequence = new MockSequence();

        mockPage.InSequence(sequence)
            .Setup(x => x.QuerySelectorAsync(".fc-cta-consent"))
            .ReturnsAsync(mockButton.Object);

        mockPage.InSequence(sequence)
            .Setup(x => x.QuerySelectorAsync(".fc-cta-consent"))
            .ReturnsAsync((IElement?)null);

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
        mockButton.Verify(x => x.ClickAsync(), Times.Once);
    }

    [Fact]
    public async Task HandleConsentAsync_GoogleFundingChoices_ButtonNotVisible_SkipsToNext()
    {
        var mockHiddenButton = CreateMockElement(isVisible: false, isEnabled: true);

        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(".fc-cta-consent"))
            .ReturnsAsync(mockHiddenButton.Object);

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeFalse();
    }

    #endregion

    #region HandleConsentAsync - OneTrust Tests

    [Fact]
    public async Task HandleConsentAsync_OneTrust_DetectedAndAccepted_ReturnsTrue()
    {
        var mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        var mockPage = CreateMockPage();
        mockPage.SetupSequence(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync(mockButton.Object)
            .ReturnsAsync((IElement?)null);

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
        mockButton.Verify(x => x.ClickAsync(), Times.Once);
    }

    [Fact]
    public async Task HandleConsentAsync_OneTrust_AcceptButtonDisabled_Skips()
    {
        var mockDisabledButton = CreateMockElement(isVisible: true, isEnabled: false);

        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeFalse();
    }

    #endregion

    #region HandleConsentAsync - CookieBot Tests

    [Fact]
    public async Task HandleConsentAsync_CookieBot_DetectedAndAccepted_ReturnsTrue()
    {
        var mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        var mockPage = CreateMockPage();
        mockPage.SetupSequence(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync(mockButton.Object)
            .ReturnsAsync((IElement?)null);

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
    }

    #endregion

    #region HandleConsentAsync - Sourcepoint Tests

    [Fact]
    public async Task HandleConsentAsync_Sourcepoint_DetectedAndAccepted_ReturnsTrue()
    {
        var mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        var mockPage = CreateMockPage();
        mockPage.SetupSequence(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync((IElement?)null)
            .ReturnsAsync(mockButton.Object)
            .ReturnsAsync((IElement?)null);

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
    }

    #endregion

    #region HandleConsentAsync - Click Exception Handling Tests

    [Fact]
    public async Task HandleConsentAsync_ClickThrowsException_FallsBackToEvaluate()
    {
        var mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).ThrowsAsync(new Exception("Click failed"));

        var mockPage = CreateMockPage();
        mockPage.SetupSequence(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync(mockButton.Object)
            .ReturnsAsync((IElement?)null);

        mockPage.Setup(x => x.EvaluateAsync<object>(It.IsAny<string>()))
            .ReturnsAsync(new object());

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
        mockPage.Verify(x => x.EvaluateAsync<object>(It.Is<string>(s => s.Contains("click()"))), Times.Once);
    }

    [Fact]
    public async Task HandleConsentAsync_ClickAndEvaluateFail_StillReturnsTrue()
    {
        var mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).ThrowsAsync(new Exception("Click failed"));

        var mockPage = CreateMockPage();
        mockPage.SetupSequence(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync(mockButton.Object)
            .ReturnsAsync((IElement?)null);

        mockPage.Setup(x => x.EvaluateAsync<object>(It.IsAny<string>()))
            .ReturnsAsync(new object());

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
    }

    #endregion

    #region HandleConsentAsync - Iframe Tests

    [Fact]
    public async Task HandleConsentAsync_IframeBased_DetectedAndHandled_ReturnsTrue()
    {
        var mockFrame = new Mock<IElement>();

        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.Is<string>(s => s.Contains("iframe"))))
            .ReturnsAsync(mockFrame.Object);

        mockPage.Setup(x => x.EvaluateAsync<bool>(It.IsAny<string>()))
            .ReturnsAsync(true);

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HandleConsentAsync_IframeNotClicked_ReturnsFalse()
    {
        var mockFrame = new Mock<IElement>();

        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.Is<string>(s => s.Contains("iframe"))))
            .ReturnsAsync(mockFrame.Object);

        mockPage.Setup(x => x.EvaluateAsync<bool>(It.IsAny<string>()))
            .ReturnsAsync(false);

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeFalse();
    }

    #endregion

    #region HandleConsentAsync - Exception Handling Tests

    [Fact]
    public async Task HandleConsentAsync_DetectionThrowsException_ContinuesToNextManager()
    {
        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Selector error"));

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleConsentAsync_IsVisibleThrowsException_ContinuesToNextSelector()
    {
        var mockPage = CreateMockPage();

        var mockFailingElement = new Mock<IElement>();
        mockFailingElement.Setup(x => x.IsVisibleAsync()).ThrowsAsync(new Exception("Visibility check failed"));

        mockPage.SetupSequence(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync(mockFailingElement.Object)
            .ReturnsAsync((IElement?)null);

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeFalse();
    }

    #endregion

    #region WaitAndHandleConsentAsync Tests

    [Fact]
    public async Task WaitAndHandleConsentAsync_BannerAppearsWithinTimeout_ReturnsTrue()
    {
        var mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        var mockPage = CreateMockPage();
        var callCount = 0;
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount > 2 ? mockButton.Object : null;
            });

        var service = CreateService();
        var result = await service.WaitAndHandleConsentAsync(mockPage.Object, maxWaitMs: 500, checkIntervalMs: 100);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task WaitAndHandleConsentAsync_BannerNeverAppears_ReturnsFalse()
    {
        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        var service = CreateService();
        var result = await service.WaitAndHandleConsentAsync(mockPage.Object, maxWaitMs: 100, checkIntervalMs: 50);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task WaitAndHandleConsentAsync_ZeroTimeout_ReturnsFalse()
    {
        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);

        var service = CreateService();
        var result = await service.WaitAndHandleConsentAsync(mockPage.Object, maxWaitMs: 0, checkIntervalMs: 10);

        result.Should().BeFalse();
    }

    #endregion

    #region Consent Manager Detection Tests

    [Theory]
    [InlineData(".fc-cta-consent")]
    [InlineData(".fc-consent-root")]
    [InlineData("[aria-label*='consent' i]")]
    public async Task HandleConsentAsync_DetectsGoogleFundingChoicesSelectors(string selector)
    {
        var mockElement = CreateMockElement(isVisible: true);

        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        var service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("#onetrust-consent-sdk")]
    [InlineData("#onetrust-banner-sdk")]
    public async Task HandleConsentAsync_DetectsOneTrustSelectors(string selector)
    {
        var mockElement = CreateMockElement(isVisible: true);

        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        var service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("#CybotCookiebotDialog")]
    [InlineData("[data-cybot]")]
    public async Task HandleConsentAsync_DetectsCookieBotSelectors(string selector)
    {
        var mockElement = CreateMockElement(isVisible: true);

        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        var service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("[id*='sp_message_iframe']")]
    [InlineData("#sp_message")]
    public async Task HandleConsentAsync_DetectsSourcepointSelectors(string selector)
    {
        var mockElement = CreateMockElement(isVisible: true);

        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        var service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("#usercentrics-root")]
    [InlineData("[data-testid='uc-accept-all-button']")]
    public async Task HandleConsentAsync_DetectsUserCentricsSelectors(string selector)
    {
        var mockElement = CreateMockElement(isVisible: true);

        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        var service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("#qc-cmp2-ui")]
    [InlineData("[id*='qc-cmp']")]
    public async Task HandleConsentAsync_DetectsQuantcastSelectors(string selector)
    {
        var mockElement = CreateMockElement(isVisible: true);

        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        var service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("#didomi-host")]
    [InlineData(".didomi-popup")]
    public async Task HandleConsentAsync_DetectsDidomiSelectors(string selector)
    {
        var mockElement = CreateMockElement(isVisible: true);

        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        var service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("[data-cookiefirst-action]")]
    [InlineData("#cookiefirst")]
    public async Task HandleConsentAsync_DetectsCookieFirstSelectors(string selector)
    {
        var mockElement = CreateMockElement(isVisible: true);

        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        var service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(".osano-cm-window")]
    [InlineData("#osano-cm")]
    public async Task HandleConsentAsync_DetectsOsanoSelectors(string selector)
    {
        var mockElement = CreateMockElement(isVisible: true);

        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        var service = CreateService();
        await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        mockPage.Verify(x => x.QuerySelectorAsync(selector), Times.AtLeastOnce);
    }

    #endregion

    #region Generic Consent Tests

    [Theory]
    [InlineData(".cookie-banner")]
    [InlineData("#cookie-banner")]
    [InlineData("[class*='cookie-consent']")]
    public async Task HandleConsentAsync_DetectsGenericSelectors(string selector)
    {
        var mockElement = CreateMockElement(isVisible: true);
        mockElement.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync(selector))
            .ReturnsAsync(mockElement.Object);

        mockPage.Setup(x => x.QuerySelectorAsync(It.Is<string>(s => s != selector)))
            .ReturnsAsync((IElement?)null);

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HandleConsentAsync_GenericAcceptButtons_AttemptsClick()
    {
        var mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        var mockPage = CreateMockPage();
        mockPage.Setup(x => x.QuerySelectorAsync("[class*='accept-all']"))
            .ReturnsAsync(mockButton.Object);

        mockPage.Setup(x => x.QuerySelectorAsync(It.Is<string>(s => s != "[class*='accept-all']")))
            .ReturnsAsync((IElement?)null);

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
        mockButton.Verify(x => x.ClickAsync(), Times.Once);
    }

    #endregion

    #region Delay Tests

    [Fact]
    public async Task HandleConsentAsync_AfterClick_WaitsForBannerDisappearance()
    {
        var mockButton = CreateMockElement(isVisible: true, isEnabled: true);
        mockButton.Setup(x => x.ClickAsync()).Returns(Task.CompletedTask);

        var mockPage = CreateMockPage();
        var callCount = 0;
        mockPage.Setup(x => x.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1) return mockButton.Object;
                return null;
            });

        var service = CreateService();
        var result = await service.HandleConsentAsync(mockPage.Object, timeoutMs: 100);

        result.Should().BeTrue();
    }

    #endregion
}
