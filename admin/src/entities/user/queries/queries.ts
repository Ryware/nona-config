import { userService } from "../api/user.service";
import { userKeys } from "./keys";

export const userQueries = {
  list: () => ({
    queryKey: userKeys.list(),
    queryFn: () => userService.getAll(),
  }),

  detail: (id: string) => ({
    queryKey: userKeys.detail(id),
    queryFn: () => userService.getById(id),
    enabled: !!id,
  }),
};
