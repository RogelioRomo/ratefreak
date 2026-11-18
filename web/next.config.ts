import type { NextConfig } from "next";
import { providerImageConfig } from "./shared/artwork/provider-images";

const nextConfig: NextConfig = {
  output: "standalone",
  images: providerImageConfig,
  cacheComponents: true,
  partialPrefetching: true,
  // Installation intentionally withholds pages until its runtime check completes.
  // Validate instant navigation on explicitly opted-in segments instead.
  experimental: { instantInsights: { validationLevel: "manual-warning" } },
};

export default nextConfig;
