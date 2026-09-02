namespace ApiGatewayService;

public class TicketsService
{
    private readonly TicketsClient _tickets;
    private readonly FlightsClient _flights;
    private readonly BonusClient _bonus;
    private readonly ICircuitBreaker _breaker;
    private readonly IBonusRefundQueue _refundQueue;


    public TicketsService(
        TicketsClient tickets,
        FlightsClient flights,
        BonusClient bonus,
        ICircuitBreaker breaker,
        IBonusRefundQueue refundQueue)
    {
        _tickets = tickets;
        _flights = flights;
        _bonus = bonus;
        _breaker = breaker;
        _refundQueue = refundQueue;
    }

    public async Task<List<TicketResponse>?> GetAllAsync(string username)
    {
        var tickets = await _breaker.ExecuteAsync(
            () => _tickets.GetAllByUserAsync(username),
            fallback: () => null,
            isCritical: true
        );

        if (tickets == null)
            return null;

        var result = new List<TicketResponse>();

        foreach (var t in tickets)
        {
            var flight = await _breaker.ExecuteAsync(
                () => _flights.GetByFlightNumberAsync(t.FlightNumber, username),
                fallback: () => new FlightResponse
                {
                    FlightNumber = t.FlightNumber,
                    FromAirport = "UNKNOWN",
                    ToAirport = "UNKNOWN",
                    Date = DateTime.MinValue,
                    Price = t.Price
                },
                isCritical: false
            );

            result.Add(new TicketResponse
            {
                TicketUid = t.TicketUid,
                FlightNumber = t.FlightNumber,
                FromAirport = flight.FromAirport,
                ToAirport = flight.ToAirport,
                Date = flight.Date == DateTime.MinValue
                    ? "UNKNOWN"
                    : flight.Date.ToString("yyyy-MM-dd HH:mm"),
                Price = t.Price,
                Status = t.Status
            });
        }

        return result;
    }

    public async Task<TicketResponse?> GetByUidAsync(Guid uid, string username)
    {
        var ticket = await _breaker.ExecuteAsync(
            () => _tickets.GetByUidAsync(uid, username),
            fallback: () => null,
            isCritical: true
        );

        if (ticket == null)
            return null;

        var flight = await GetFlightSafeAsync(ticket.FlightNumber, username);

        return new TicketResponse
        {
            TicketUid = ticket.TicketUid,
            FlightNumber = ticket.FlightNumber,
            FromAirport = flight!.FromAirport,
            ToAirport = flight.ToAirport,
            Date = flight.Date == DateTime.MinValue
                ? "UNKNOWN"
                : flight.Date.ToString("yyyy-MM-dd HH:mm"),
            Price = ticket.Price,
            Status = ticket.Status
        };
    }

    private async Task<FlightResponse?> GetFlightSafeAsync(
        string flightNumber,
        string username)
    {
        return await _breaker.ExecuteAsync(
            () => _flights.GetByFlightNumberAsync(flightNumber, username),
            fallback: () => new FlightResponse
            {
                FlightNumber = flightNumber,
                FromAirport = "UNKNOWN",
                ToAirport = "UNKNOWN",
                Date = DateTime.MinValue,
                Price = 0
            },
            isCritical: false
        );
    }

    public async Task<TicketPurchaseResponse?> PurchaseAsync(
        string username,
        TicketPurchaseRequest dto)
    {
        var flight = await _breaker.ExecuteAsync(
            () => _flights.GetByFlightNumberAsync(dto.FlightNumber, username),
            fallback: () => null,
            isCritical: true
        );

        if (flight == null)
            return null;

        var ticket = await _breaker.ExecuteAsync(
            () => _tickets.PurchaseAsync(username, dto),
            fallback: () => null,
            isCritical: true
        );

        if (ticket == null)
            return null;

        ApplyBonusResponse? bonus = null;

        try
        {
            bonus = await _breaker.ExecuteAsync(
                () => _bonus.ApplyAsync(username, new ApplyBonusRequest
                {
                    TicketUid = ticket.TicketUid,
                    Price = dto.Price,
                    PaidFromBalance = dto.PaidFromBalance
                }),
                fallback: () => null,
                isCritical: false
            );

            if (bonus == null)
                throw new Exception("Bonus service failed");
        }
        catch (Exception)
        {
            await _breaker.ExecuteAsync(
                () => _tickets.CancelAsync(ticket.TicketUid, username),
                fallback: () => false,
                isCritical: false
            );

            return null;
        }

        var privilege = await _breaker.ExecuteAsync(
            () => _bonus.GetPrivilegeAsync(username),
            fallback: () => null,
            isCritical: false
        );

        return new TicketPurchaseResponse
        {
            TicketUid = ticket.TicketUid,
            FlightNumber = dto.FlightNumber,
            FromAirport = flight.FromAirport,
            ToAirport = flight.ToAirport,
            Date = flight.Date.ToString("yyyy-MM-dd HH:mm"),
            Price = dto.Price,
            PaidByMoney = bonus.PaidByMoney,
            PaidByBonuses = bonus.PaidByBonuses,
            Status = ticket.Status ?? "PAID",
            Privilege = privilege == null
                ? null
                : new PrivilegeShortInfo
                {
                    Balance = privilege.Balance,
                    Status = privilege.Status
                }
        };
    }


    public async Task<bool> CancelAsync(Guid ticketUid, string username, string? authorizationHeader)
    {
        var canceled = await _breaker.ExecuteAsync(
            () => _tickets.CancelAsync(ticketUid, username),
            fallback: () => false,
            isCritical: true
        );

        if (!canceled)
            return false;

        var refundResult = await _breaker.ExecuteAsync(
            () => _bonus.RefundAsync(username, ticketUid),
            fallback: () => RefundResult.Retry,
            isCritical: false
        );

        if (refundResult is RefundResult.Success or RefundResult.NotNeeded)
            return true;

        await _refundQueue.EnqueueAsync(
            new BonusRefundJob(username, ticketUid, authorizationHeader)
        );

        return true;
    }
}
