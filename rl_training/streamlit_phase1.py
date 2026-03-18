"""
Single-page Streamlit dashboard for Phase 1 / Phase 2 PPO visualization.

Usage:
    streamlit run rl_training/streamlit_phase1.py
"""
from __future__ import annotations

import csv
import json
from pathlib import Path

import pandas as pd
import streamlit as st


PROJECT_ROOT = Path(__file__).resolve().parent.parent
LOG_DIR = PROJECT_ROOT / "logs" / "phase1"
TRAINING_LOG = LOG_DIR / "training_log.csv"
EVAL_SUMMARY_LOG = LOG_DIR / "eval_summary.csv"
EVAL_MATCH_LOG = LOG_DIR / "eval_match_results.jsonl"


def load_csv(path: Path) -> pd.DataFrame:
    if not path.exists():
        return pd.DataFrame()
    return pd.read_csv(path)


def load_jsonl(path: Path) -> pd.DataFrame:
    if not path.exists():
        return pd.DataFrame()

    rows = []
    with path.open(encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            rows.append(json.loads(line))
    return pd.DataFrame(rows)


def latest_non_null(df: pd.DataFrame, column: str):
    if column not in df.columns:
        return None
    series = df[column].dropna()
    if series.empty:
        return None
    return series.iloc[-1]


def main():
    st.set_page_config(page_title="PPO Phase1 Dashboard", layout="wide")
    st.title("PPO AI Phase 2 Visualization")
    st.caption("单页总览：训练趋势 + 评估结果 + 对局列表")

    training_df = load_csv(TRAINING_LOG)
    eval_summary_df = load_csv(EVAL_SUMMARY_LOG)
    eval_match_df = load_jsonl(EVAL_MATCH_LOG)

    if training_df.empty:
        st.warning(f"未找到训练日志：{TRAINING_LOG}")
        return

    latest_iteration = int(training_df["iteration"].max())
    latest_eval_win = latest_non_null(training_df, "eval_win_rate")
    latest_eval_illegal = latest_non_null(training_df, "eval_illegal_rate")
    latest_avg_reward = latest_non_null(training_df, "avg_reward")

    col1, col2, col3, col4 = st.columns(4)
    col1.metric("Latest Iteration", latest_iteration)
    col2.metric("Latest Train Reward", f"{latest_avg_reward:.4f}" if latest_avg_reward is not None else "N/A")
    col3.metric("Latest Eval WinRate", f"{latest_eval_win:.2%}" if latest_eval_win is not None else "N/A")
    col4.metric("Latest Eval Illegal", f"{latest_eval_illegal:.2%}" if latest_eval_illegal is not None else "N/A")

    st.subheader("Training Trends")
    train_chart_df = training_df[["iteration", "avg_reward", "policy_loss", "value_loss"]].set_index("iteration")
    st.line_chart(train_chart_df, height=260)

    eval_chart_source = eval_summary_df if not eval_summary_df.empty else training_df.dropna(subset=["eval_win_rate"])
    if not eval_chart_source.empty:
        st.subheader("Evaluation Trends")
        eval_chart_df = eval_chart_source[["iteration", "eval_win_rate", "eval_illegal_rate", "eval_avg_reward"]].set_index("iteration")
        st.line_chart(eval_chart_df, height=260)

    st.subheader("Recent Evaluation Summary")
    if eval_summary_df.empty:
        st.info("还没有评估汇总数据。训练跑到评估点后会生成。")
    else:
        st.dataframe(eval_summary_df.sort_values("iteration", ascending=False), use_container_width=True, hide_index=True)

    st.subheader("Evaluation Match List")
    if eval_match_df.empty:
        st.info("还没有评估对局明细。")
        return

    iterations = sorted(eval_match_df["iteration"].dropna().unique().tolist())
    selected_iteration = st.selectbox("Filter by iteration", options=["All"] + iterations, index=0)
    result_filter = st.selectbox("Filter by result", options=["All", "Win", "Loss"], index=0)

    filtered_df = eval_match_df.copy()
    if selected_iteration != "All":
        filtered_df = filtered_df[filtered_df["iteration"] == selected_iteration]
    if result_filter == "Win":
        filtered_df = filtered_df[filtered_df["won"] == True]
    elif result_filter == "Loss":
        filtered_df = filtered_df[filtered_df["won"] == False]

    display_columns = [
        "iteration",
        "seed",
        "won",
        "reward",
        "illegal_count",
        "action_count",
        "my_team_final_score",
        "my_team_level_gain",
        "defender_score",
        "next_dealer",
        "timestamp_utc",
    ]
    available_columns = [col for col in display_columns if col in filtered_df.columns]
    st.dataframe(
        filtered_df[available_columns].sort_values(["iteration", "seed"], ascending=[False, True]),
        use_container_width=True,
        hide_index=True,
    )


if __name__ == "__main__":
    main()
