// Exact canonical format emitted by the MusicBrainz adapter. No arbitrary URLs.
export function isMusicBrainzCover(reference: string): boolean {
  const match = /^https:\/\/coverartarchive\.org\/release-group\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\/front-500$/.exec(reference);
  return !!match && match[0] === reference && match[1] !== "00000000-0000-0000-0000-000000000000";
}
