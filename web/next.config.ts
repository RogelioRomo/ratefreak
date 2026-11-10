import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  cacheComponents: true,
  partialPrefetching: true,
  // Installation intentionally withholds pages until its runtime check completes.
  // Validate instant navigation on explicitly opted-in segments instead.
  experimental: { instantInsights: { validationLevel: "manual-warning" } },
};

export default nextConfig;
