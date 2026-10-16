// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/middleware.py

namespace Nooa.Runtime;

/// <summary>Calls the rest of the middleware chain and returns the context.</summary>
public delegate Task<TContext> MiddlewareNext<TContext>(TContext context);

/// <summary>
/// Middleware that wraps one lifecycle operation. It receives the context and the rest of the chain.
/// It must return the context (<c>return await next(context)</c>, or set the result and return the context).
/// </summary>
public delegate Task<TContext> Middleware<TContext>(TContext context, MiddlewareNext<TContext> next);
