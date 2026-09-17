using Sub.Api.Models;
using Sub.Api.Services;
using Xunit;

namespace Sub.Api.Tests;

public class FalsePositiveDetectionTests
{
    [Fact]
    public void Detect_PayPalExpiredCardEmail_DoesNotCreateSubscription()
    {
        var service =
            new SubscriptionDetectionService();

        var email =
            new EmailCandidate
            {
                GmailMessageId =
                    "paypal-expired-card-test",

                From =
                    "\"service@intl.paypal.com\" <service@intl.paypal.com>",

                Subject =
                    "Update your expired debit or credit card information for PayPal",

                Snippet =
                    "Please update your card information soon. " +
                    "The card linked to your PayPal account has expired.",

                BodyText =
                    """
                    Update your expired debit or credit card information for PayPal.

                    The card linked to your PayPal account has expired.

                    Since it is the only payment method in your account,
                    you will need to update your card information
                    to continue using PayPal.

                    Please update your card expiration date
                    and card security code.

                    Update card details.

                    PayPal
                    """,

                Date =
                    new DateTime(
                        2026,
                        3,
                        1,
                        23,
                        25,
                        26,
                        DateTimeKind.Utc)
            };

        var result =
            service.Detect(email);

        Assert.False(
            result.IsSubscription);

        Assert.Equal(
            "Unknown",
            result.EventType);

        Assert.Equal(
            "Low",
            result.Confidence);

        Assert.Null(
            result.SubscriptionName);

        Assert.Null(
            result.PlanName);
    }

    [Fact]
    public void Detect_AegeanTicketReceipt_DoesNotCreateSubscription()
    {
        var service =
            new SubscriptionDetectionService();

        var email =
            new EmailCandidate
            {
                GmailMessageId =
                    "aegean-ticket-receipt-test",

                From =
                    "RES OFFICE <noreply@aegeanair.com>",

                Subject =
                    "AEGEAN AIRLINES Electronic Ticket Receipt",

                Snippet =
                    "Electronic ticket receipt. " +
                    "Booking confirmed. Payment details and fare information.",

                BodyText =
                    """
                    AEGEAN AIRLINES

                    ELECTRONIC TICKET RECEIPT

                    Passenger ticket information.

                    Booking status: OK

                    Fare details

                    Fare: EUR 200.00

                    Taxes and charges apply.

                    PAYMENT DETAILS

                    Total Amount: EUR 200.00

                    Electronic Ticket

                    Carriage and other services provided by the carrier
                    are subject to the airline conditions of carriage.
                    """,

                Date =
                    new DateTime(
                        2025,
                        12,
                        30,
                        8,
                        56,
                        15,
                        DateTimeKind.Utc)
            };

        var result =
            service.Detect(email);

        Assert.False(
            result.IsSubscription);

        Assert.Equal(
            "Unknown",
            result.EventType);

        Assert.Equal(
            "Low",
            result.Confidence);

        Assert.Null(
            result.SubscriptionName);

        Assert.Null(
            result.PlanName);
    }
}