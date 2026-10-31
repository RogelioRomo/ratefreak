// @vitest-environment jsdom
import { useQueryClient, type QueryClient } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { expect, it } from "vitest";
import type { QueryProbeProps } from "@/test/interfaces/query-probe-props";
import { Providers } from "./providers";

function Probe({ onClient }: QueryProbeProps) {
  const client = useQueryClient();
  onClient(client);
  return <p>{client.getQueryData<string>(["probe"]) ?? "empty"}</p>;
}

it("preserves the mounted provider's client and data on rerender", () => {
  let client!: QueryClient;
  const onClient = (value: QueryClient) => { client = value; };
  const tree = render(<Providers><Probe onClient={onClient} /></Providers>);
  const original = client;
  client.setQueryData(["probe"], "saved");
  tree.rerender(<Providers><Probe onClient={onClient} /></Providers>);
  expect(client).toBe(original);
  expect(screen.getByText("saved")).toBeInTheDocument();
  client.clear();
});

it("does not share cache between independent provider trees", () => {
  let first!: QueryClient;
  let second!: QueryClient;
  render(<Providers><Probe onClient={(client) => { first = client; }} /></Providers>);
  first.setQueryData(["probe"], "private");
  render(<Providers><Probe onClient={(client) => { second = client; }} /></Providers>);
  expect(second).not.toBe(first);
  expect(second.getQueryData(["probe"])).toBeUndefined();
  first.clear(); second.clear();
});
