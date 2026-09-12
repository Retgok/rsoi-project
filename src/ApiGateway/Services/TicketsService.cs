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
            "tickets",
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
                "flights",
                async () =>
                {
                    var f = await _flights.GetByFlightNumberAsync(t.FlightNumber, username);
                    return f ?? throw new InvalidOperationException("Flight unavailable");
                },
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
            "tickets",
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
            "flights",
            async () =>
            {
                var f = await _flights.GetByFlightNumberAsync(flightNumber, username);
                return f ?? throw new InvalidOperationException("Flight unavailable");
            },
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

    public async Task<PurchaseResult> PurchaseAsync(
        string username,
        TicketPurchaseRequest dto)
    {
        var flight = await _breaker.ExecuteAsync(
            "flights",
            () => _flights.GetByFlightNumberAsync(dto.FlightNumber, username),
            fallback: () => null,
            isCritical: true
        );

        if (flight == null)
            return PurchaseResult.Unavailable();

        var ticket = await _breaker.ExecuteAsync(
            "tickets",
            () => _tickets.PurchaseAsync(username, dto),
            fallback: () => null,
            isCritical: true
        );

        if (ticket == null)
            return PurchaseResult.Unavailable();

        var incremented = await _breaker.ExecuteAsync(
            "flights",
            async () => await _flights.TryIncrementBoughtAsync(dto.FlightNumber),
            fallback: () => null,
            isCritical: true
        );

        if (incremented == null)
            return PurchaseResult.Unavailable();
        if (incremented == false)
            return PurchaseResult.SoldOut();

        ApplyBonusResponse? bonus = null;

        try
        {
            bonus = await _breaker.ExecuteAsync(
                "bonus",
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
                "tickets",
                () => _tickets.CancelAsync(ticket.TicketUid, username),
                fallback: () => false,
                isCritical: false
            );
            await _flights.DecrementBoughtAsync(dto.FlightNumber);
            return PurchaseResult.Unavailable();
        }

        var privilege = await _breaker.ExecuteAsync(
            "bonus",
            () => _bonus.GetPrivilegeAsync(username),
            fallback: () => null,
            isCritical: false
        );

        return PurchaseResult.Ok(new TicketPurchaseResponse
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
        });
    }


    public async Task<bool> CancelAsync(Guid ticketUid, string username, string? authorizationHeader)
    {
        var ticket = await _breaker.ExecuteAsync(
            "tickets",
            () => _tickets.GetByUidAsync(ticketUid, username),
            fallback: () => null,
            isCritical: true
        );

        if (ticket == null || !string.Equals(ticket.Status, "PAID", StringComparison.OrdinalIgnoreCase))
            return false;

        var flightNumber = ticket.FlightNumber;

        var canceled = await _breaker.ExecuteAsync(
            "tickets",
            () => _tickets.CancelAsync(ticketUid, username),
            fallback: () => false,
            isCritical: true
        );

        if (!canceled)
            return false;

        await _breaker.ExecuteAsync(
            "flights",
            async () =>
            {
                await _flights.DecrementBoughtAsync(flightNumber);
                return (bool?)true;
            },
            fallback: () => false,
            isCritical: false
        );

        var refundResult = await _breaker.ExecuteAsync(
            "bonus",
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

public sealed class PurchaseResult
{
    public enum Kind { Success, SoldOut, Unavailable }

    public Kind Outcome { get; init; }
    public TicketPurchaseResponse? Response { get; init; }

    public static PurchaseResult Ok(TicketPurchaseResponse response)
        => new() { Outcome = Kind.Success, Response = response };

    public static PurchaseResult SoldOut()
        => new() { Outcome = Kind.SoldOut };

    public static PurchaseResult Unavailable()
        => new() { Outcome = Kind.Unavailable };
}
