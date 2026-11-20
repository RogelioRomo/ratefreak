import {beforeEach,expect,it,vi} from "vitest";
import {getHistory} from "./history";
const mock=vi.fn<typeof fetch>();beforeEach(()=>{mock.mockReset();vi.stubGlobal('fetch',mock);});
it('requests the selected period and forwards cancellation',async()=>{mock.mockResolvedValue(Response.json({year:2025}));const signal=new AbortController().signal;await expect(getHistory({year:2025,month:2,page:2,pageSize:20},signal)).resolves.toMatchObject({year:2025});expect(mock.mock.calls[0][0]).toBe('/api/v1/users/me/library/history?year=2025&month=2&page=2&pageSize=20');expect(mock.mock.calls[0][1]?.signal).toBe(signal);expect(mock.mock.calls[0][1]?.cache).toBe('no-store');});
it('propagates private read errors',async()=>{mock.mockResolvedValue(Response.json({},{status:401}));await expect(getHistory({page:1,pageSize:20})).rejects.toMatchObject({status:401});});
