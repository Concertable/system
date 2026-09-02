namespace Concertable.E2E;

public sealed record Profile(Surface Surface, Endpoints Endpoints)
{
    public static Profile B2B(
        string serviceApi,
        string searchApi,
        string auth,
        string paymentApi) =>
        new(Surface.B2B, new(serviceApi, searchApi, auth, paymentApi));

    public static Profile Customer(
        string serviceApi,
        string searchApi,
        string auth,
        string paymentApi) =>
        new(Surface.Customer, new(serviceApi, searchApi, auth, paymentApi));
}
