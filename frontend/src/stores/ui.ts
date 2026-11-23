import { create } from "zustand";
export const useUI = create<{
  career: string;
  select: (id: string) => void;
  collapsed: boolean;
  toggle: () => void;
}>((set) => ({
  career: localStorage.getItem("bm-career") ?? "",
  select: (id) => {
    localStorage.setItem("bm-career", id);
    set({ career: id });
  },
  collapsed: false,
  toggle: () => set((s) => ({ collapsed: !s.collapsed })),
}));
