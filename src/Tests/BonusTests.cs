using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using BonusService;
using TestsSupport;

public class BonusControllerTests
{
    private readonly Mock<IBonusRepo> _repoMock;
    private readonly BonusController _controller;
    private const string TestUsername = "test_user";
    private readonly Guid TestTicketUid = new("4b14d59a-2423-4555-8938-1a5c60959828");

    public BonusControllerTests()
    {
        _repoMock = new Mock<IBonusRepo>();
        _controller = new BonusController(_repoMock.Object, ControllerTestHelper.CreateEventPublisherMock().Object);
        ControllerTestHelper.SetUser(_controller, TestUsername);
    }

    [Fact]
    public async Task Get_ReturnsOk_WhenPrivilegeExists()
    {
        var privilege = new Privilege { Username = TestUsername, Balance = 150, Status = "SILVER" };
        _repoMock.Setup(r => r.GetByUsernameAsync(TestUsername)).ReturnsAsync(privilege);

        var result = await _controller.Get();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PrivilegeResponse>(okResult.Value);
        Assert.Equal(150, response.Balance);
    }

    [Fact]
    public async Task Get_CreatesNewPrivilege_WhenMissing()
    {
        _repoMock.Setup(r => r.GetByUsernameAsync(TestUsername)).ReturnsAsync((Privilege?)null);

        var result = await _controller.Get();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PrivilegeResponse>(okResult.Value);
        Assert.Equal(0, response.Balance);
        _repoMock.Verify(r => r.AddPrivilegeAsync(It.IsAny<Privilege>()), Times.Once);
    }

    [Fact]
    public async Task ApplyBonus_ReturnsOk_WhenPaidFromBalance()
    {
        var privilege = new Privilege { Id = 1, Username = TestUsername, Balance = 500, Status = "BRONZE" };
        _repoMock.Setup(r => r.GetByUsernameAsync(TestUsername)).ReturnsAsync(privilege);
        _repoMock.Setup(r => r.UpdatePrivilegeAsync(It.IsAny<Privilege>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.AddHistoryAsync(It.IsAny<PrivilegeHistory>())).Returns(Task.CompletedTask);

        var req = new ApplyBonusRequest { TicketUid = TestTicketUid, Price = 1500, PaidFromBalance = true };
        var result = await _controller.ApplyBonus(req);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Refund_ReturnsNoContent_WhenHistoryExists()
    {
        var privilege = new Privilege { Id = 1, Username = TestUsername, Balance = 500, Status = "BRONZE" };
        var history = new PrivilegeHistory
        {
            Privilege = privilege,
            BalanceDiff = -500,
            OperationType = "DEBIT_THE_ACCOUNT"
        };
        _repoMock.Setup(r => r.GetLastHistoryByTicketAsync(TestTicketUid)).ReturnsAsync(history);
        _repoMock.Setup(r => r.UpdatePrivilegeAsync(It.IsAny<Privilege>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.AddHistoryAsync(It.IsAny<PrivilegeHistory>())).Returns(Task.CompletedTask);

        var result = await _controller.Refund(TestTicketUid);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(1000, privilege.Balance);
        _repoMock.Verify(r => r.AddHistoryAsync(It.Is<PrivilegeHistory>(h =>
            h.OperationType == "FILL_IN_BALANCE" && h.BalanceDiff == 500)), Times.Once);
    }

    [Fact]
    public async Task Refund_RemovesBonus_WhenTicketWasPaidWithoutBonuses()
    {
        var privilege = new Privilege { Id = 1, Username = TestUsername, Balance = 650, Status = "BRONZE" };
        var history = new PrivilegeHistory
        {
            Privilege = privilege,
            BalanceDiff = 150,
            OperationType = "FILL_IN_BALANCE"
        };
        _repoMock.Setup(r => r.GetLastHistoryByTicketAsync(TestTicketUid)).ReturnsAsync(history);
        _repoMock.Setup(r => r.UpdatePrivilegeAsync(It.IsAny<Privilege>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.AddHistoryAsync(It.IsAny<PrivilegeHistory>())).Returns(Task.CompletedTask);

        var result = await _controller.Refund(TestTicketUid);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(500, privilege.Balance);
        _repoMock.Verify(r => r.AddHistoryAsync(It.Is<PrivilegeHistory>(h =>
            h.OperationType == "DEBIT_THE_ACCOUNT" && h.BalanceDiff == 150)), Times.Once);
    }
}
