"use client";

import { useState } from "react";
import { QueryClientProvider } from "@tanstack/react-query";
import type { ChildrenProps } from "@/shared/interfaces/children-props";
import { createQueryClient } from "@/shared/query/create-query-client";

export function Providers({ children }: ChildrenProps) {
  const [client] = useState(createQueryClient);
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}
