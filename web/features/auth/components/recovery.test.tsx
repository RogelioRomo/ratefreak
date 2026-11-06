// @vitest-environment jsdom
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { act } from "@testing-library/react";
import { focusManager, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, it, vi } from "vitest";
import type { ReactNode } from "react";
import { createQueryClient } from "@/shared/query/create-query-client";
import { resetCsrfState } from "@/shared/api/csrf";
import { SessionProvider } from "./session-provider";
import { RegisterScreen } from "./register-screen";
import { ResetPasswordScreen } from "./reset-password-screen";
import { RecoverySettings } from "./recovery-settings";

const account = { id: "a", handle: "reader", displayName: "A Reader", email: "reader@example.test", createdAt: "2026-10-30T00:00:00Z" };
const code = "12345678-90ABCDEF-12345678-90ABCDEF";
let signedIn = false;
let resetStatus = 200;
let hasPassword = true;
let hasRecoveryCode = true;
const fetchMock = vi.fn<typeof fetch>();
beforeEach(() => {
  signedIn = false; resetStatus = 200; hasPassword = true; hasRecoveryCode = true;
  resetCsrfState(); fetchMock.mockReset();
  fetchMock.mockImplementation(async url => {
    const path = String(url);
    if (path.endsWith("auth/csrf")) return Response.json({ requestToken: "token" });
    if (path.endsWith("users/me")) return signedIn ? Response.json(account) : new Response(null, { status: 401 });
    if (path.endsWith("auth/register")) return Response.json({ ...account, recoveryCode: code }, { status: 201 });
    if (path.endsWith("auth/recovery-status")) return Response.json({ hasPassword, hasRecoveryCode });
    if (path.endsWith("auth/recovery-code")) return Response.json({ recoveryCode: code });
    if (path.endsWith("auth/reset-password")) return resetStatus === 200 ? Response.json({ recoveryCode: code }) : Response.json({ title: "Unable to recover account." }, { status: resetStatus });
    throw new Error(`Unexpected request: ${path}`);
  });
  vi.stubGlobal("fetch", fetchMock);
});
function mount(children: ReactNode) {
  const client = createQueryClient();
  const view = render(<QueryClientProvider client={client}><SessionProvider>{children}</SessionProvider></QueryClientProvider>);
  return { client, ...view };
}
async function fillReset() {
  fireEvent.change(await screen.findByLabelText("Handle"), { target: { value: account.handle } });
  fireEvent.change(screen.getByLabelText("Recovery code"), { target: { value: "AAAAAAAA-AAAAAAAA-AAAAAAAA-AAAAAAAA" } });
  fireEvent.change(screen.getByLabelText("New password"), { target: { value: "Replacement-password-83" } });
  fireEvent.change(screen.getByLabelText("Confirm new password"), { target: { value: "Replacement-password-83" } });
}
it("registration requires saving a code before offering login and does not cache secrets", async () => {
  const { client } = mount(<RegisterScreen />);
  fireEvent.change(await screen.findByLabelText("Display name"), { target: { value: account.displayName } });
  fireEvent.change(screen.getByLabelText("Handle"), { target: { value: account.handle } });
  fireEvent.change(screen.getByLabelText("Email"), { target: { value: account.email } });
  fireEvent.change(screen.getByLabelText("Password"), { target: { value: "Registration-password-72" } });
  fireEvent.submit(screen.getByRole("form", { name: "Create account" }));
  expect(await screen.findByText(code)).toBeVisible();
  expect(screen.queryByRole("link", { name: "Log in" })).not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Continue to login" })).toBeDisabled();
  fireEvent.click(screen.getByRole("checkbox", { name: "I saved my recovery code" }));
  fireEvent.click(screen.getByRole("button", { name: "Continue to login" }));
  expect(screen.getByRole("link", { name: "Log in" })).toHaveAttribute("href", "/login");
  expect(screen.queryByText(code)).not.toBeInTheDocument();
  expect(JSON.stringify(client.getMutationCache().getAll().map(m => m.state))).not.toMatch(/12345678|Registration-password/);
  expect(signedIn).toBe(false);
});
it("resets using the handle and saved code then displays a replacement without caching it", async () => {
  const { client } = mount(<ResetPasswordScreen />);
  await fillReset();
  fireEvent.submit(screen.getByRole("form", { name: "Reset password" }));
  expect(await screen.findByText(code)).toBeVisible();
  const calls = fetchMock.mock.calls.filter(([url]) => String(url).endsWith("auth/reset-password"));
  expect(calls).toHaveLength(1);
  expect(JSON.parse(calls[0][1]!.body as string)).toEqual({ handle: "reader", recoveryCode: "AAAAAAAA-AAAAAAAA-AAAAAAAA-AAAAAAAA", password: "Replacement-password-83" });
  expect(JSON.stringify(client.getMutationCache().getAll().map(m => m.state))).not.toMatch(/12345678|AAAAAAAA|Replacement-password/);
  expect(screen.queryByLabelText("New password")).not.toBeInTheDocument();
  expect(signedIn).toBe(false);
});
it("clears submitted secrets on failure and explains lost-response recovery without automatically retrying", async () => {
  resetStatus = 502; mount(<ResetPasswordScreen />); await fillReset();
  fireEvent.submit(screen.getByRole("form", { name: "Reset password" }));
  expect(await screen.findByRole("alert")).toHaveTextContent(/try logging in with your new password/i);
  expect(screen.getByLabelText("Recovery code")).toHaveValue("");
  expect(screen.getByLabelText("New password")).toHaveValue("");
  expect(screen.getByLabelText("Confirm new password")).toHaveValue("");
  expect(fetchMock.mock.calls.filter(([url]) => String(url).endsWith("auth/reset-password"))).toHaveLength(1);
});
it("rejects mismatched password confirmation before submitting", async () => {
  mount(<ResetPasswordScreen />); await fillReset();
  fireEvent.change(screen.getByLabelText("Confirm new password"), { target: { value: "Different-password-83" } });
  fireEvent.submit(screen.getByRole("form", { name: "Reset password" }));
  expect(await screen.findByRole("alert")).toHaveTextContent(/passwords do not match/i);
  expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith("auth/reset-password"))).toBe(false);
});
it.each([true, false])("supports password-confirmed enrollment/replacement (already enrolled: %s)", async enrolled => {
  signedIn = true; hasRecoveryCode = enrolled;
  const { client } = mount(<RecoverySettings />);
  fireEvent.change(await screen.findByLabelText("Current password"), { target: { value: "Original-password-72" } });
  fireEvent.submit(screen.getByRole("form", { name: "Recovery code settings" }));
  expect(await screen.findByText(code)).toBeVisible();
  expect(screen.queryByLabelText("Current password")).not.toBeInTheDocument();
  expect(JSON.stringify(client.getMutationCache().getAll().map(m => m.state))).not.toMatch(/12345678|Original-password/);
  fireEvent.click(screen.getByRole("checkbox", { name: "I saved my recovery code" }));
  fireEvent.click(screen.getByRole("button", { name: "Done" }));
  await waitFor(() => expect(screen.queryByText(code)).not.toBeInTheDocument());
});
it("external-only accounts keep provider recovery instead of local password enrollment", async () => {
  signedIn = true; hasPassword = false; mount(<RecoverySettings />);
  expect(await screen.findByText(/recover access through your linked sign-in provider/i)).toBeVisible();
  expect(screen.queryByLabelText("Current password")).not.toBeInTheDocument();
});

it.each(["registration", "reset"])("keeps the unsaved code across a background session failure (%s)", async flow => {
  const { client } = mount(flow === "registration" ? <RegisterScreen /> : <ResetPasswordScreen />);
  if (flow === "registration") {
    fireEvent.change(await screen.findByLabelText("Display name"), { target: { value: account.displayName } });
    fireEvent.change(screen.getByLabelText("Handle"), { target: { value: account.handle } });
    fireEvent.change(screen.getByLabelText("Email"), { target: { value: account.email } });
    fireEvent.change(screen.getByLabelText("Password"), { target: { value: "Registration-password-72" } });
    fireEvent.submit(screen.getByRole("form", { name: "Create account" }));
  } else { await fillReset(); fireEvent.submit(screen.getByRole("form", { name: "Reset password" })); }
  expect(await screen.findByText(code)).toBeVisible();
  const original = fetchMock.getMockImplementation()!;
  fetchMock.mockImplementation((url, options) => String(url).endsWith("users/me") ? Promise.resolve(new Response(null, { status: 503 })) : original(url, options));
  act(() => { focusManager.setFocused(false); focusManager.setFocused(true); });
  await waitFor(() => expect(client.getQueryState(["session"])?.status).toBe("error"));
  expect(screen.getByText(code)).toBeVisible();
  focusManager.setFocused(undefined);
});
