# GraphSdkRetryHandler
Showcasing an issue with the RetryHandler not retrying for Http Status 504 (GatewayTimeout).
Using Fiddler or an equivalent product, block the request after response and edit it to return a 504 instead.
Example: 
```
HTTP/1.1 504 Gateway Timeout
Date: Fri, 25 Jan 2019 16:49:29 GMT
FiddlerTemplate: True
Content-Type: text/html
Content-Length: 172

Fiddler: HTTP/504 gateway timeout.
```
OR 
```
StatusCode: 504, ReasonPhrase: 'Gateway Timeout', Version: 1.1, Content: System.Net.Http.StreamContent, Headers:
{
  Transfer-Encoding: chunked
  request-id: 4e7b6099-8697-4acd-ab01-919e409666d9
  client-request-id: 4e7b6099-8697-4acd-ab01-919e409666d9
  x-ms-ags-diagnostic: {"ServerInfo":{"DataCenter":"North Central US","Slice":"SliceC","Ring":"3","ScaleUnit":"003","RoleInstance":"AGSFE_IN_13","ADSiteName":"NCU"}}
  Duration: 10680.543
  Strict-Transport-Security: max-age=31536000
  Cache-Control: private
  Date: Thu, 05 Sep 2019 17:37:27 GMT
  Content-Type: application/json
}
```

The Retry handler will fail to retry and throw an exception saying that the request content has been disposed.
`RetryHandler.cs` --> `SendRetryAsync` --> 
```
// general clone request with internal CloneAsync (see CloneAsync for details) extension method 
var request = await response.RequestMessage.CloneAsync();
```
seems to be the cause of this exception when debugging the SDK.

If we bypass this method and retry manually, we can eventually reach the `HttpProvider` --> `SendAsync` --> `SendRequestAsync` --> `httpClient.SendAsync()`
but we always get a `TaskCanceledException` which is eventually converted to ServiceException with code `timeout`. This behaviour has been introduced in version 1.16.
Using the version 1.14 of the SDK, the request was retried but since the version 1.15 it would systematically throw a `TaskCanceledException`.

# How to debug this solution
Please replace the Graph Token with a valid one in the Resource file before debugging the Solution.
