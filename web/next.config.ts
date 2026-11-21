import type { NextConfig } from "next";
import { providerImageConfig } from "./shared/artwork/provider-images";

import {buildIdentity} from "./shared/build/identity";

const nextConfig: NextConfig = {
  output: "standalone",
  env: {RATEFREAK_BUILD_VERSION: buildIdentity.version, RATEFREAK_BUILD_REVISION: buildIdentity.revision},
  images: providerImageConfig,
  cacheComponents: true,
  partialPrefetching: true,
  // Installation intentionally withholds pages until its runtime check completes.
  // Validate instant navigation on explicitly opted-in segments instead.
  experimental: { instantInsights: { validationLevel: "manual-warning" } },
};

export default nextConfig;
