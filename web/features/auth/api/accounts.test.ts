import { beforeEach, expect, it, vi } from "vitest";
import { getCurrentAccount, login, register, logout } from "./accounts";
import { resetCsrfState } from "@/shared/api/csrf";

const fetchMock = vi.fn<typeof fetch>();
const profile = { id: "account-a", handle: "reader", displayName: "A Reader", email: "reader@example.test", createdAt: "2026-10-30T00:00:00Z", bio: null };
beforeEach(() => { resetCsrfState(); fetchMock.mockReset(); vi.stubGlobal("fetch", fetchMock); });

it("maps only an unauthenticated response to a signed-out session", async () => {
  fetchMock.mockResolvedValueOnce(new Response(null, { status: 401 }));
  expect(await getCurrentAccount()).toBeNull();
  for (const status of [403, 429, 503]) {
    fetchMock.mockResolvedValueOnce(Response.json({ title: "Unavailable." }, { status }));
    await expect(getCurrentAccount()).rejects.toMatchObject({ status });
  }
});

it("matches existing account endpoints and never signs in after registration", async () => {
  const input = { email: profile.email, password: "Example-password-72", handle: profile.handle, displayName: profile.displayName };
  fetchMock.mockResolvedValueOnce(Response.json({ requestToken: "token" })).mockResolvedValueOnce(Response.json(profile, { status: 201 }));
  expect(await register(input)).toEqual(profile);
  expect(fetchMock.mock.calls.map(([url]) => url)).toEqual(["/api/v1/auth/csrf", "/api/v1/auth/register"]);
  fetchMock.mockResolvedValueOnce(Response.json({ requestToken: "new" })).mockResolvedValueOnce(Response.json(profile));
  expect(await login({ email: input.email, password: input.password })).toEqual(profile);
  expect(fetchMock.mock.calls[3][0]).toBe("/api/v1/auth/login");
});

it("treats an expired logout as signed out but propagates failed logout", async () => {
  fetchMock.mockResolvedValueOnce(Response.json({ requestToken: "token" })).mockResolvedValueOnce(new Response(null, { status: 401 }));
  await expect(logout()).resolves.toBeUndefined();
  fetchMock.mockResolvedValueOnce(Response.json({ requestToken: "token" })).mockResolvedValueOnce(new Response(null, { status: 503 }));
  await expect(logout()).rejects.toMatchObject({ status: 503 });
});
