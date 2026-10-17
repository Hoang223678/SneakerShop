export function isAdminUser(user) {
  return user?.role === "Admin";
}

export function homePathFor(user) {
  return isAdminUser(user) ? "/admin" : "/";
}
