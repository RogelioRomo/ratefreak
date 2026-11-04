import { IconStarFilled } from "@tabler/icons-react";
import type { CommunityRatingProps } from "../interfaces/community-rating-props";
import styles from "./star-rating.module.css";
export function CommunityRating({ aggregate }: CommunityRatingProps) {
 if (aggregate.count === 0 || aggregate.average === null) return <p>No ratings yet.</p>;
 const rounded = Math.round(Math.max(0, Math.min(5, aggregate.average)) * 2) / 2;
 return <>
  <div className={styles.stars} role="img" aria-label={`${rounded} out of 5 stars, rounded to the nearest half star`}>
   {Array.from({ length: 5 }, (_, index) => {
    const fill = Math.max(0, Math.min(1, rounded - index));
    return <span className={styles.star} key={index} data-community-star>
     <IconStarFilled className={styles.empty} size={36} aria-hidden="true" />
     <span className={`${styles.fill} ${fill === 1 ? styles.full : fill === 0.5 ? styles.halfFilled : styles.unfilled}`}><IconStarFilled size={36} aria-hidden="true" /></span>
    </span>;
   })}
  </div>
  <p>{aggregate.average.toFixed(2)} / 5 · {aggregate.count} {aggregate.count === 1 ? "rating" : "ratings"}</p>
 </>;
}
