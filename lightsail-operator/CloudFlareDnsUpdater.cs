using CloudFlare.Client;
using CloudFlare.Client.Api.Result;
using CloudFlare.Client.Api.Zones.DnsRecord;
using CloudFlare.Client.Enumerators;

namespace lightsail_operator;

internal static class CloudFlareResultExtensions
{
    public static T EnsureSuccess<T>(this CloudFlareResult<T> result, Func<string, Exception> exceptionProvider)
    {
        if (result.Success)
        {
            return result.Result;
        }

        throw exceptionProvider(result.Errors[0].Message);
    }
}

public class CloudFlareDnsUpdater(string emailAddress, string globalApiKey, string zoneId)
{
    private readonly CloudFlareClient _client = new(emailAddress, globalApiKey);

    public async Task UpdateDns(string name, string ip)
    {
        var dnsRecords = (await _client.Zones.DnsRecords.GetAsync(zoneId, new DnsRecordFilter
        {
            Name = name,
            Type = DnsRecordType.A
        })).EnsureSuccess(msg => new CloudFlareDnsRecordGetException(msg));

        var dnsRecord = dnsRecords.FirstOrDefault(x => x.Name == name);

        if (dnsRecord == null)
        {
            (await _client.Zones.DnsRecords.AddAsync(zoneId, new NewDnsRecord
            {
                Name = name,
                Type = DnsRecordType.A,
                Content = ip,
                Proxied = false
            })).EnsureSuccess(msg => new CloudFlareDnsRecordAddException(msg));
        }
        else
        {
            (await _client.Zones.DnsRecords.UpdateAsync(zoneId, dnsRecord.Id, new ModifiedDnsRecord
            {
                Name = name,
                Type = DnsRecordType.A,
                Content = ip,
                Proxied = false
            })).EnsureSuccess(msg => new CloudFlareDnsRecordUpdateException(msg));
        }
    }
}