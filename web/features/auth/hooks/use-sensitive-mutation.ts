"use client";

import { useRef } from "react";
import { useMutation } from "@tanstack/react-query";
import type { SensitiveMutationResult } from "../interfaces/sensitive-mutation-result";

export function useSensitiveMutation<TInput extends object>(execute: (input: TInput) => Promise<void>): SensitiveMutationResult<TInput> {
  const input = useRef<TInput | null>(null);
  const mutation = useMutation({
    mutationFn: async () => {
      if (!input.current) throw new Error("Missing account request.");
      await execute(input.current);
    },
    gcTime: 0,
    retry: false,
  });

  async function submit(value: TInput): Promise<void> {
    if (input.current) throw new Error("An account action is already in progress.");
    input.current = value;
    try { await mutation.mutateAsync(); }
    finally { input.current = null; }
  }

  return { submit, isPending: mutation.isPending, isSuccess: mutation.isSuccess, error: mutation.error, reset: mutation.reset };
}
