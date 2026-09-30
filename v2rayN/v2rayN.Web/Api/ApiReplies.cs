using v2rayN.Web.Contracts;

namespace v2rayN.Web.Api;

public static class ApiReplies
{
    public static IResult Ok<T>(T data, string messageKey = ApiMessageKeys.CommonLoaded) =>
        Results.Ok(ApiEnvelope<T>.Ok(data, messageKey));

    public static IResult Operation(
        OperationView result,
        int successStatus = StatusCodes.Status200OK,
        int failureStatus = StatusCodes.Status400BadRequest) =>
        Results.Json(result, statusCode: result.Success ? successStatus : failureStatus);

    public static IResult NotFound(string code, string messageKey) =>
        Results.Json(ApiEnvelope<object>.Fail(code, messageKey), statusCode: StatusCodes.Status404NotFound);

    public static IResult Conflict(string code, string messageKey, object? data = null) =>
        Results.Json(ApiEnvelope<object>.Fail(code, messageKey, data), statusCode: StatusCodes.Status409Conflict);
}
