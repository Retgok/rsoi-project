using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using TicketService;
using TestsSupport;

public class TicketsControllerTests
{
    private readonly Mock<ITicketRepo> _repoMock;
    private readonly TicketsController _controller;
    private const string TestUsername = "test_user";
    private readonly Guid TestTicketUid = new("4b14d59a-2423-4555-8938-1a5c60959828");

    public TicketsControllerTests()
    {
        _repoMock = new Mock<ITicketRepo>();
        _controller = new TicketsController(_repoMock.Object, ControllerTestHelper.CreateEventPublisherMock().Object);
        ControllerTestHelper.SetUser(_controller, TestUsername);
    }

    [Fact]
    public async Task GetAll_ReturnsOk_WithTicketsList()
    {
        var tickets = new List<Ticket>
        {
            new() { TicketUid = Guid.NewGuid(), FlightNumber = "SU201", Price = 1000, Status = "PAID" },
            new() { TicketUid = Guid.NewGuid(), FlightNumber = "UT501", Price = 2000, Status = "PAID" }
        };
        _repoMock.Setup(r => r.GetAllByUserAsync(TestUsername)).ReturnsAsync(tickets);

        var result = await _controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsAssignableFrom<IEnumerable<TicketResponse>>(okResult.Value);
        Assert.Equal(2, response.Count());
    }

    [Fact]
    public async Task GetAll_ReturnsUnauthorized_WhenUsernameIsMissing()
    {
        var controller = new TicketsController(_repoMock.Object, ControllerTestHelper.CreateEventPublisherMock().Object);
        var result = await controller.GetAll();
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetByUid_ReturnsOk_WhenTicketExists()
    {
        var ticket = new Ticket { TicketUid = TestTicketUid, Username = TestUsername, FlightNumber = "SU201", Price = 1000, Status = "PAID" };
        _repoMock.Setup(r => r.GetByUidAsync(TestTicketUid, TestUsername)).ReturnsAsync(ticket);

        var result = await _controller.GetByUid(TestTicketUid);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Purchase_ReturnsOk_WhenValid()
    {
        var dto = new TicketPurchaseRequest { FlightNumber = "SU201", Price = 1000, PaidFromBalance = false };
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Ticket>())).ReturnsAsync((Ticket t) => t);

        var result = await _controller.Purchase(dto);

        Assert.IsType<OkObjectResult>(result);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Ticket>()), Times.Once);
    }

    [Fact]
    public async Task Cancel_ReturnsNoContent_WhenTicketExists()
    {
        var ticket = new Ticket { TicketUid = TestTicketUid, Username = TestUsername, FlightNumber = "SU201", Price = 1000, Status = "PAID" };
        _repoMock.Setup(r => r.GetByUidAsync(TestTicketUid, TestUsername)).ReturnsAsync(ticket);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<Ticket>())).Returns(Task.CompletedTask);

        var result = await _controller.Cancel(TestTicketUid);

        Assert.IsType<NoContentResult>(result);
    }
}
