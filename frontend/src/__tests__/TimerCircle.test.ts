import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import TimerCircle from '../components/game/TimerCircle.vue'

const SIZE = 120
const STROKE = 10
const R = (SIZE - STROKE) / 2
const CIRC = 2 * Math.PI * R

describe('TimerCircle', () => {
  it('renders the seconds label', () => {
    const wrapper = mount(TimerCircle, { props: { seconds: 15, total: 20 } })
    expect(wrapper.text()).toContain('15')
  })

  it('displays full arc at seconds === total', () => {
    const wrapper = mount(TimerCircle, { props: { seconds: 20, total: 20 } })
    const circle = wrapper.findAll('circle')[1] // progress ring is second
    // fraction = 1, progress = CIRC * (1 - 1) = 0
    const offset = parseFloat(circle.attributes('stroke-dashoffset') ?? 'NaN')
    expect(offset).toBeCloseTo(0, 1)
  })

  it('displays empty arc at seconds === 0', () => {
    const wrapper = mount(TimerCircle, { props: { seconds: 0, total: 20 } })
    const circle = wrapper.findAll('circle')[1]
    const offset = parseFloat(circle.attributes('stroke-dashoffset') ?? 'NaN')
    // fraction = 0, progress = CIRC * (1 - 0) = CIRC
    expect(offset).toBeCloseTo(CIRC, 1)
  })

  it('displays half arc at seconds === total/2', () => {
    const wrapper = mount(TimerCircle, { props: { seconds: 10, total: 20 } })
    const circle = wrapper.findAll('circle')[1]
    const offset = parseFloat(circle.attributes('stroke-dashoffset') ?? 'NaN')
    expect(offset).toBeCloseTo(CIRC * 0.5, 1)
  })

  it('clamps negative seconds to 0 arc', () => {
    const wrapper = mount(TimerCircle, { props: { seconds: -5, total: 20 } })
    const circle = wrapper.findAll('circle')[1]
    const offset = parseFloat(circle.attributes('stroke-dashoffset') ?? 'NaN')
    expect(offset).toBeCloseTo(CIRC, 1)
  })

  it('uses green colour when more than 50% time remains', () => {
    const wrapper = mount(TimerCircle, { props: { seconds: 15, total: 20 } }) // 75%
    const circle = wrapper.findAll('circle')[1]
    expect(circle.attributes('stroke')).toBe('#26890C')
  })

  it('uses orange colour between 25% and 50%', () => {
    const wrapper = mount(TimerCircle, { props: { seconds: 8, total: 20 } }) // 40%
    const circle = wrapper.findAll('circle')[1]
    expect(circle.attributes('stroke')).toBe('#FFA602')
  })

  it('uses red colour when ≤25% time remains', () => {
    const wrapper = mount(TimerCircle, { props: { seconds: 4, total: 20 } }) // 20%
    const circle = wrapper.findAll('circle')[1]
    expect(circle.attributes('stroke')).toBe('#E21B3C')
  })

  it('renders two circles (background + progress)', () => {
    const wrapper = mount(TimerCircle, { props: { seconds: 10, total: 20 } })
    expect(wrapper.findAll('circle')).toHaveLength(2)
  })

  it('sets stroke-dasharray to full circumference', () => {
    const wrapper = mount(TimerCircle, { props: { seconds: 10, total: 20 } })
    const circle = wrapper.findAll('circle')[1]
    const dasharray = parseFloat(circle.attributes('stroke-dasharray') ?? 'NaN')
    expect(dasharray).toBeCloseTo(CIRC, 1)
  })
})
