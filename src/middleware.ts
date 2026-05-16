import { NextRequest, NextResponse } from 'next/server'

const NET_API_URL = process.env.NET_API_URL || 'http://localhost:5000'

export function middleware(req: NextRequest) {
  const target = new URL(req.nextUrl.pathname + req.nextUrl.search, NET_API_URL)
  return NextResponse.rewrite(target)
}

export const config = {
  matcher: '/api/:path*'
}
