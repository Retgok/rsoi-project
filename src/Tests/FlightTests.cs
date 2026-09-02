using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using FlightService;
using TestsSupport;

public class FlightsControllerTests
{
    private readonly Mock<IFlightRepo> _repoMock;
    private readonly FlightsController _controller;
    private const string TestFlightNumber = "SU201";
    private const string TestUsername = "test_user";

    private readonly Airport TestAirportA = new() { Id = 1, Name = "Sheremetyevo", City = "Moscow", Country = "Russia" };
    private readonly Airport TestAirportB = new() { Id = 2, Name = "Pulkovo", City = "St. Petersburg", Country = "Russia" };

    public FlightsControllerTests()
    {
        _repoMock = new Mock<IFlightRepo>();
        _controller = new FlightsController(_repoMock.Object, ControllerTestHelper.CreateEventPublisherMock().Object);
        ControllerTestHelper.SetUser(_controller, TestUsername);
    }

    [Fact]
    public async Task GetAll_ReturnsOk_WithFlightsListAndFormattedAirports()
    {
        var flights = new List<Flight>
        {
            new() { FlightNumber = TestFlightNumber, Price = 1000, FromAirport = TestAirportA, ToAirport = TestAirportB, DateTime = DateTime.Now },
            new() { FlightNumber = "UT501", Price = 2000, FromAirport = TestAirportB, ToAirport = TestAirportA, DateTime = DateTime.Now }
        };
        _repoMock.Setup(r => r.GetAllAsync(1, 100)).ReturnsAsync(flights);

        var result = await _controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsAssignableFrom<IEnumerable<FlightResponse>>(okResult.Value).ToList();
        Assert.Equal(2, response.Count);
        Assert.Equal("Moscow Sheremetyevo", response[0].FromAirport);
        Assert.Equal("St. Petersburg Pulkovo", response[0].ToAirport);
    }

    [Fact]
    public async Task GetByFlightNumber_ReturnsOk_WhenFlightExistsAndFormatsAirport()
    {
        var flight = new Flight
        {
            FlightNumber = TestFlightNumber,
            Price = 1000,
            FromAirport = TestAirportA,
            ToAirport = TestAirportB,
            DateTime = new DateTime(2025, 12, 13)
        };
        _repoMock.Setup(r => r.GetByFlightNumberAsync(TestFlightNumber)).ReturnsAsync(flight);

        var result = await _controller.GetByFlightNumber(TestFlightNumber);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<FlightResponse>(okResult.Value);
        Assert.Equal(TestFlightNumber, response.FlightNumber);
    }

    [Fact]
    public async Task GetByFlightNumber_ReturnsNotFound_WhenFlightMissing()
    {
        _repoMock.Setup(r => r.GetByFlightNumberAsync(TestFlightNumber)).ReturnsAsync((Flight?)null);
        var result = await _controller.GetByFlightNumber(TestFlightNumber);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetByFlightNumber_ReturnsNotFound_WhenFlightMissing_SecondCase()
    {
        _repoMock.Setup(r => r.GetByFlightNumberAsync("MISSING")).ReturnsAsync((Flight?)null);
        var result = await _controller.GetByFlightNumber("MISSING");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Create_ReturnsCreated_ForAdmin()
    {
        ControllerTestHelper.SetUser(_controller, "admin", "Admin");
        _repoMock.Setup(r => r.GetByFlightNumberAsync("NEW001")).ReturnsAsync((Flight?)null);
        _repoMock.Setup(r => r.GetAirportByIdAsync(1)).ReturnsAsync(TestAirportA);
        _repoMock.Setup(r => r.GetAirportByIdAsync(2)).ReturnsAsync(TestAirportB);
        _repoMock.Setup(r => r.AddFlightAsync(It.IsAny<Flight>()))
            .ReturnsAsync((Flight f) =>
            {
                f.Id = 10;
                f.FromAirport = TestAirportA;
                f.ToAirport = TestAirportB;
                return f;
            });

        var result = await _controller.Create(new CreateFlightRequest
        {
            FlightNumber = "NEW001",
            DateTime = DateTime.UtcNow,
            FromAirportId = 1,
            ToAirportId = 2,
            Price = 2000,
            Capacity = 120
        });

        var created = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<FlightResponse>(created.Value);
        Assert.Equal("NEW001", response.FlightNumber);
        Assert.Equal(120, response.Capacity);
    }
}
