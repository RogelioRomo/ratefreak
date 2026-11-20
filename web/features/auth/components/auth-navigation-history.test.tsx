// @vitest-environment jsdom
import {render,screen} from "@testing-library/react";
import {expect,it,vi} from "vitest";
import {AuthNavigation} from "./auth-navigation";
vi.mock('next/navigation',()=>({usePathname:()=>'/library/history'}));
vi.mock('../hooks/use-session',()=>({useSession:()=>({account:{id:'owner'}})}));
vi.mock('@/features/notifications/components/notification-link',()=>({NotificationLink:()=>null}));
vi.mock('./logout-control',()=>({LogoutControl:()=>null}));
it('keeps Library selected on the nested History page',()=>{render(<AuthNavigation/>);expect(screen.getByRole('link',{name:'Library'})).toHaveAttribute('aria-current','page');});
