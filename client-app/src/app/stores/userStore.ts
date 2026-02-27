import { makeAutoObservable, runInAction } from "mobx";
import { User, UserFormValues } from "../models/user";
import agent from "../api/agent";
import { store } from "./store";
import router from "../router/Routes";

export default class UserStore {
  user: User | null = null;
  fbLoading = false;
  refreshTokenTimeout?: number | NodeJS.Timeout;

  constructor() {
    makeAutoObservable(this);
  }

  get isLoggedIn() {
    return !!this.user;
  }

  login = async (creds: UserFormValues) => {
    try {
      const user = await agent.Auth.login(creds);
      store.commonStore.setToken(user.token);
      this.startRefreshTokenTimer(user);
      runInAction(() => (this.user = user));
      router.navigate("/activities");
      store.modalStore.closeModal();
    } catch (error) {
      console.log(error);
      throw error;
    }
  };

  register = async (creds: UserFormValues) => {
    try {
      const user = await agent.Auth.register(creds);
      store.commonStore.setToken(user.token);
      this.startRefreshTokenTimer(user);
      runInAction(() => (this.user = user));
      router.navigate("/activities");
      /* Email verification is disabled in production 
      await agent.Auth.register(creds);
      router.navigate(`/auth/registerSuccess?email=${creds.email}`); */
      store.modalStore.closeModal();
    } catch (error) {
      if (Array.isArray(error)) throw error;
      store.modalStore.closeModal();
      console.log(500);
    }
  };

  logout = () => {
    store.commonStore.setToken(null);
    this.user = null;
    router.navigate("/");
  };

  getUser = async () => {
    try {
      const user = await agent.Auth.current();
      store.commonStore.setToken(user.token);
      runInAction(() => (this.user = user));
      this.startRefreshTokenTimer(user);
    } catch (error) {
      console.log(error);
    }
  };

  setImage = (image: string) => {
    if (this.user) this.user.image = image;
  };

  setDisplayName = (name: string) => {
    if (this.user) this.user.displayName = name;
  };

  facebookLogin = async (accessToken: string) => {
    this.fbLoading = true;
    try {
      const user = await agent.Auth.fbLogin(accessToken);
      store.commonStore.setToken(user.token);
      this.startRefreshTokenTimer(user);
      runInAction(() => {
        this.user = user;
        this.fbLoading = false;
      });
      router.navigate("/activities");
    } catch (error) {
      console.log(error);
      runInAction(() => {
        this.fbLoading = false;
      });
    }
  };

  refreshToken = async () => {
    this.stopRefreshTokenTimer();
    try {
      const user = await agent.Auth.refreshToken();
      runInAction(() => (this.user = user));
      store.commonStore.setToken(user.token);
      this.startRefreshTokenTimer(user);
    } catch (error) {
      console.log(error);
    }
  };

  private startRefreshTokenTimer(user: User) {
    const jwtToken = JSON.parse(atob(user.token.split(".")[1]));
    const expires = new Date(jwtToken.exp * 1000);
    const timeout = expires.getTime() - Date.now() - (60 * 1000);
    this.refreshTokenTimeout = setTimeout(this.refreshToken, timeout);
  }

  private stopRefreshTokenTimer() {
    clearTimeout(this.refreshTokenTimeout);
  }
}
