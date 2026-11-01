// @vitest-environment jsdom
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { focusManager, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, it, vi } from "vitest";
import type { ReactNode } from "react";
import { createQueryClient } from "@/shared/query/create-query-client";
import { resetCsrfState } from "@/shared/api/csrf";
import { SessionProvider } from "./session-provider";
import { LoginScreen } from "./login-screen";
import { RegisterScreen } from "./register-screen";
import { AccountScreen } from "./account-screen";

const navigation = vi.hoisted(() => ({ replace: vi.fn(), push: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => navigation }));
const account = { id: "a", handle: "reader", displayName: "A Reader", email: "reader@example.test", createdAt: "2026-10-30T00:00:00Z" };
let signedIn = false;
let loginStatus = 200;
let registerStatus = 201;
let logoutStatus = 204;
const fetchMock = vi.fn<typeof fetch>();

beforeEach(() => {
  resetCsrfState(); signedIn = false; loginStatus = 200; registerStatus = 201; logoutStatus = 204;
  navigation.replace.mockReset(); navigation.push.mockReset(); fetchMock.mockReset();
  fetchMock.mockImplementation(async (url) => {
    const path = String(url);
    if (path.endsWith("auth/csrf")) return Response.json({ requestToken: "token" });
    if (path.endsWith("users/me")) return signedIn ? Response.json(account) : new Response(null, { status: 401 });
    if (path.endsWith("auth/login")) {
      if (loginStatus === 200) { signedIn = true; return Response.json(account); }
      return Response.json({ title: "Login failed." }, { status: loginStatus, headers: { "Retry-After": "60" } });
    }
    if (path.endsWith("auth/register")) return Response.json(registerStatus === 201 ? account : { title: "Invalid.", errors: { handle: ["Handle is invalid."] } }, { status: registerStatus });
    if (path.endsWith("auth/logout")) {
      if (logoutStatus === 204) { signedIn = false; return new Response(null, { status: 204 }); }
      return new Response(null, { status: logoutStatus });
    }
    throw new Error("Unexpected test URL");
  });
  vi.stubGlobal("fetch", fetchMock);
});

function mount(children: ReactNode) {
  const client = createQueryClient();
  return render(<QueryClientProvider client={client}><SessionProvider>{children}</SessionProvider></QueryClientProvider>);
}

async function fillLogin() {
  fireEvent.change(await screen.findByLabelText("Email"), { target: { value: account.email } });
  fireEvent.change(screen.getByLabelText("Password"), { target: { value: "Example-password-72" } });
}

it("logs in through labeled controls and navigates to the protected account", async () => {
  mount(<LoginScreen />);
  await fillLogin();
  expect(screen.getByLabelText("Password")).toHaveAttribute("autocomplete", "current-password");
  fireEvent.submit(screen.getByRole("form", { name: "Log in" }));
  await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/account"));
  expect(screen.queryByLabelText("Password")).not.toBeInTheDocument();
});

it.each([401, 429])("shows useful login failure guidance for HTTP %i", async (status) => {
  loginStatus = status; mount(<LoginScreen />); await fillLogin();
  fireEvent.submit(screen.getByRole("form", { name: "Log in" }));
  const error = await screen.findByRole("alert");
  expect(error).toHaveTextContent(status === 401 ? "Email or password is incorrect." : "60 seconds");
  expect(navigation.replace).not.toHaveBeenCalled();
  expect(screen.getByLabelText("Email")).toHaveValue(account.email);
  expect(screen.getByLabelText("Password")).toHaveValue("");
});

it("registration shows success without signing in", async () => {
  mount(<RegisterScreen />);
  fireEvent.change(await screen.findByLabelText("Display name"), { target: { value: account.displayName } });
  fireEvent.change(screen.getByLabelText("Handle"), { target: { value: account.handle } });
  await fillLogin();
  fireEvent.submit(screen.getByRole("form", { name: "Create account" }));
  expect(await screen.findByRole("heading", { name: "Your account is ready." })).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "Log in" })).toHaveAttribute("href", "/login");
  expect(signedIn).toBe(false);
  expect(navigation.replace).not.toHaveBeenCalled();
});

it("associates server field errors with registration controls", async () => {
  registerStatus = 400; mount(<RegisterScreen />);
  fireEvent.change(await screen.findByLabelText("Display name"), { target: { value: account.displayName } });
  fireEvent.change(screen.getByLabelText("Handle"), { target: { value: account.handle } });
  await fillLogin();
  fireEvent.submit(screen.getByRole("form", { name: "Create account" }));
  await screen.findByRole("alert");
  expect(screen.getByLabelText("Handle")).toHaveAttribute("aria-invalid", "true");
  expect(screen.getByLabelText("Handle")).toHaveAccessibleDescription(/Handle is invalid/);
});

it("does not render private data before session resolution and redirects anonymous visitors", async () => {
  mount(<AccountScreen />);
  expect(screen.queryByText(account.email)).not.toBeInTheDocument();
  await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/login"));
  expect(screen.queryByText(account.email)).not.toBeInTheDocument();
});

it("shows a protected profile and does not claim success on logout failure", async () => {
  signedIn = true; logoutStatus = 503; mount(<AccountScreen />);
  expect(await screen.findByText(account.email)).toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "Log out" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Please try again.");
  expect(signedIn).toBe(true);
  expect(navigation.replace).not.toHaveBeenCalled();
});

it("redirects a successful protected-page logout to login without competing navigation", async () => {
  signedIn = true; mount(<AccountScreen />);
  await screen.findByText(account.email);
  fireEvent.click(screen.getByRole("button", { name: "Log out" }));
  await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/login"));
  expect(navigation.replace).not.toHaveBeenCalledWith("/");
});

it("keeps form input mounted during background session checks", async () => {
  mount(<LoginScreen />); await fillLogin();
  const original = fetchMock.getMockImplementation()!;
  let resolve!: (response: Response) => void;
  fetchMock.mockImplementation((url, options) => String(url).endsWith("users/me")
    ? new Promise((done) => { resolve = done; }) : original(url, options));
  act(() => { focusManager.setFocused(false); focusManager.setFocused(true); });
  await waitFor(() => expect(resolve).toBeTypeOf("function"));
  expect(screen.getByLabelText("Email")).toHaveValue(account.email);
  await act(async () => { resolve(new Response(null, { status: 401 })); });
  focusManager.setFocused(undefined);
});

it("disables pending submissions and sends a double submit only once", async () => {
  mount(<LoginScreen />); await fillLogin();
  const original = fetchMock.getMockImplementation()!;
  let resolve!: (response: Response) => void;
  fetchMock.mockImplementation((url, options) => String(url).endsWith("auth/login")
    ? new Promise((done) => { resolve = done; }) : original(url, options));
  const form = screen.getByRole("form", { name: "Log in" });
  fireEvent.submit(form); fireEvent.submit(form);
  await waitFor(() => expect(resolve).toBeTypeOf("function"));
  expect(screen.getByRole("button", { name: "Logging in…" })).toBeDisabled();
  expect(fetchMock.mock.calls.filter(([url]) => String(url).endsWith("auth/login"))).toHaveLength(1);
  await act(async () => { signedIn = true; resolve(Response.json(account)); });
});

it("returns to a canonical provider preview after login", async () => {
  mount(<LoginScreen returnTo="/catalog/discover/tmdb/movie/11" />);
  await fillLogin(); fireEvent.submit(screen.getByRole("form", { name: "Log in" }));
  await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/catalog/discover/tmdb/movie/11"));
});
