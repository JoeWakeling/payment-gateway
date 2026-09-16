# Assumptions

## 1. Cards expiring in the current month are valid

A card whose expiry date falls in the current month is accepted. This is the industry standard — a card remains valid until the end of its expiry month.

The end of the month is evaluated in UTC. The request carries no issuer timezone, so near a month boundary a card may be treated as expired a few hours early or late relative to local time. This has been simplified for the sake of this task; in reality, cutoff hours may vary by gateway and issuer.

## 2. Retrievable payments are those that reached the acquiring bank

> "A merchant should be able to retrieve the details of a previously made payment"

"Made" in this context refers to payments where an attempt it made to process them with the acquiring bank. Requests rejected at validation never make it that far, so they are not stored and cannot be retrieved.

## 3. Rejected requests do not echo back the original request

Unlike a successful payment, a request rejected at validation does not return the original contents of the request. In some cases it may not be possible — e.g. if validation fails because data is missing, there is nothing to return - so validation error messages are returned instead.

A rejection is conveyed by the HTTP status code (`400` or `422`) and a problem details body, rather than a response with a `Status` of `Rejected`.

## 4. Any failure to get a decision from the bank results in a declined payment

If the acquiring bank is unavailable, the payment is **Declined** rather than **Rejected**. The request data has already passed validation, but authorisation could not be established, so the payment has failed.

The same applies to timeouts, network errors and unexpected responses. After a timeout the bank may in fact have authorised the payment; reconciling this is out of scope for this task.

## 5. No authentication or merchant scoping

Any caller with a payment id can retrieve that payment, and payments are not tied to a merchant. In reality, merchants would be authenticated and only able to retrieve their own payments.

## 6. Amount must be greater than zero

The brief only requires an integer, but zero and negative amounts are rejected as they don't represent a valid payment.

## 7. Supported currencies all use two decimal places

For simplicity, the three supported currencies (GBP, USD, EUR) were chosen as they all use two decimal places for their minor unit, so no per-currency handling is needed (e.g. JPY has zero, BHD has three).

## 8. No Luhn check on card numbers

A Luhn check is standard practice, but it isn't required by the brief and the acquiring bank validates the card. Card numbers must contain digits only — spaces or dashes are rejected rather than stripped.

## 9. Expiry year is a four-digit year

A two-digit year such as `26` is treated as the year 26, not 2026, so the card is rejected as expired.

## 10. The authorisation code is not stored

The authorisation code returned by the bank isn't mentioned in the brief and isn't used for anything, so it is not stored or returned. If the gateway were extended (e.g. capture, refunds or disputes), it would likely be required.
